using System.Security.Cryptography;
using Bubox.Application.DTOs.Order;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

public class OrderService(
    IOrderRepository orderRepository,
    IOrderUnitOfWorkFactory unitOfWorkFactory,
    IPaymentRepository paymentRepository,
    IMenuAssignmentRepository assignmentRepository,
    IUserAddressRepository addressRepository,
    IUserRepository userRepository,
    ISettingsService settingsService,
    IPaymentGateway paymentGateway,
    IVoucherService voucherService,
    IOutletRepository outletRepository,
    DeliveryCoverageService coverageService,
    OrderStatusEngine statusEngine) : IOrderService
{
    private const string OrderNumberAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<CreateOrderResponse> CreateOrderAsync(OrderActor actor, CreateOrderRequest request)
    {
        var userId = actor.UserId ?? throw new UnauthorizedAccessException("Sesi pengguna tidak valid.");
        var nowUtc = DateTime.UtcNow;
        var settings = await settingsService.GetEffectiveOrderSettingsAsync();

        // 1. Validasi dasar
        var fulfillmentType = request.FulfillmentType?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!FulfillmentTypes.All.Contains(fulfillmentType))
        {
            throw new InvalidOrderException("Metode pengambilan harus DELIVERY atau PICKUP.");
        }

        if (request.Days == null || request.Days.Count == 0)
        {
            throw new InvalidOrderException("Pilih minimal satu tanggal kirim.");
        }

        if (request.Days.Count > OrderLimits.MaxDaysPerOrder)
        {
            throw new InvalidOrderException($"Maksimal {OrderLimits.MaxDaysPerOrder} tanggal kirim per pesanan.");
        }

        if (request.Days.Select(d => d.DeliveryDate).Distinct().Count() != request.Days.Count)
        {
            throw new InvalidOrderException("Tanggal kirim tidak boleh duplikat.");
        }

        if (request.Notes?.Length > 500)
        {
            throw new InvalidOrderException("Catatan maksimal 500 karakter.");
        }

        // 2. Cutoff & rentang tanggal
        var latestDate = settings.GetLocalToday(nowUtc).AddDays(OrderLimits.MaxAdvanceDays);
        var passedCutoff = request.Days
            .Select(d => d.DeliveryDate)
            .Where(date => !settings.IsOrderable(date, nowUtc))
            .OrderBy(date => date)
            .ToList();

        if (passedCutoff.Count > 0)
        {
            var dates = string.Join(", ", passedCutoff.Select(d => d.ToString("dd-MM-yyyy")));
            throw new OrderCutoffPassedException(
                $"Tanggal {dates} sudah melewati batas pemesanan (H-{settings.CutoffDaysBefore} pukul {settings.CutoffTime:HH\\:mm}).");
        }

        var tooFar = request.Days.Where(d => d.DeliveryDate > latestDate).Select(d => d.DeliveryDate).ToList();
        if (tooFar.Count > 0)
        {
            throw new InvalidOrderException(
                $"Pemesanan hanya bisa untuk {OrderLimits.MaxAdvanceDays} hari ke depan (paling lambat {latestDate:dd-MM-yyyy}).");
        }

        // 3. Alamat (snapshot) untuk DELIVERY, wajib di dalam area pengantaran
        Guid? addressId = null;
        string? addressSnapshot = null;
        string? deliveryWilayahCode = null;
        if (fulfillmentType == FulfillmentTypes.Delivery)
        {
            if (request.AddressId is null || request.AddressId == Guid.Empty)
            {
                throw new InvalidOrderException("Alamat pengiriman wajib dipilih untuk metode DELIVERY.");
            }

            var address = await addressRepository.GetByIdAsync(request.AddressId.Value, userId)
                ?? throw new InvalidOrderException("Alamat pengiriman tidak ditemukan.");

            deliveryWilayahCode = await coverageService.EnsureWilayahCodeAsync(address);
            if (deliveryWilayahCode == null)
            {
                throw new DeliveryNotCoveredException(
                    "Wilayah alamat ini tidak dikenali. Ubah alamat dan pilih kecamatan & kelurahan dari daftar.");
            }

            if (!await coverageService.IsCoveredAsync(deliveryWilayahCode))
            {
                throw new DeliveryNotCoveredException();
            }

            addressId = address.Id;
            addressSnapshot = OrderMapper.SerializeAddress(new AddressSnapshotDto(
                address.Id, address.Label, address.RecipientName, address.PhoneNumber, address.FullAddress,
                address.Subdistrict, address.City, address.Province, address.PostalCode, address.Notes,
                address.Latitude, address.Longitude, deliveryWilayahCode));
        }

        // 3b. Outlet (snapshot) untuk PICKUP, wajib outlet aktif
        Guid? outletId = null;
        string? outletSnapshot = null;
        if (fulfillmentType == FulfillmentTypes.Pickup)
        {
            if (request.OutletId is null || request.OutletId == Guid.Empty)
            {
                throw new InvalidOrderException("Outlet tempat ambil wajib dipilih untuk metode PICKUP.");
            }

            var outlet = await outletRepository.GetByIdAsync(request.OutletId.Value);
            if (outlet is not { IsActive: true })
            {
                throw new OutletUnavailableException();
            }

            outletId = outlet.Id;
            outletSnapshot = OrderMapper.SerializeOutlet(new OutletSnapshotDto(
                outlet.Id, outlet.Name, outlet.FullAddress, outlet.WilayahPath, OutletService.FormatPickupHours(outlet),
                outlet.WhatsappNumber, outlet.Latitude, outlet.Longitude));
        }

        // 4. Harga dihitung ulang dari jadwal menu aktif (bukan dari client)
        var weekly = (await assignmentRepository.GetAllWeeklyAssignmentsAsync())
            .GroupBy(a => (a.DayOfWeek, a.MenuId))
            .ToDictionary(g => g.Key, g => g.First());

        var orderId = Guid.NewGuid();
        var days = new List<OrderDay>();
        var items = new List<OrderItem>();

        foreach (var dayRequest in request.Days.OrderBy(d => d.DeliveryDate))
        {
            var date = dayRequest.DeliveryDate;
            var dayOfWeek = OrderSettings.ToDayOfWeekNumber(date);
            var dayLabel = $"{OrderSettings.GetDayName(date)} {date:dd-MM-yyyy}";

            if (dayRequest.Items == null || dayRequest.Items.Count == 0)
            {
                throw new InvalidOrderException($"Belum ada menu yang dipilih untuk {dayLabel}.");
            }

            if (dayRequest.Items.Count > OrderLimits.MaxItemsPerDay)
            {
                throw new InvalidOrderException($"Maksimal {OrderLimits.MaxItemsPerDay} menu per hari ({dayLabel}).");
            }

            if (dayRequest.Items.Select(i => i.MenuId).Distinct().Count() != dayRequest.Items.Count)
            {
                throw new InvalidOrderException($"Menu yang sama tercantum lebih dari sekali pada {dayLabel}.");
            }

            var day = new OrderDay
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                DeliveryDate = OrderMapper.ToDbDate(date),
                DayOfWeek = dayOfWeek,
                StatusCode = OrderDayStatusCodes.Scheduled,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };

            foreach (var itemRequest in dayRequest.Items)
            {
                if (itemRequest.Qty < OrderLimits.MinQtyPerItem || itemRequest.Qty > OrderLimits.MaxQtyPerItem)
                {
                    throw new InvalidOrderException(
                        $"Jumlah porsi harus {OrderLimits.MinQtyPerItem}-{OrderLimits.MaxQtyPerItem} per menu ({dayLabel}).");
                }

                if (!weekly.TryGetValue((dayOfWeek, itemRequest.MenuId), out var menu))
                {
                    throw new InvalidOrderException($"Salah satu menu tidak tersedia pada {dayLabel}. Muat ulang menu lalu coba lagi.");
                }

                var lineTotal = menu.Price * itemRequest.Qty;
                items.Add(new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderDayId = day.Id,
                    MenuId = menu.MenuId,
                    MenuName = menu.Name,
                    CategoryId = menu.CategoryId,
                    CategoryCode = menu.CategoryCode,
                    CategoryName = menu.CategoryName,
                    UnitPrice = menu.Price,
                    Qty = itemRequest.Qty,
                    LineTotal = lineTotal,
                    CreatedAt = nowUtc
                });
                day.DaySubtotal += lineTotal;
            }

            days.Add(day);
        }

        var subtotal = days.Sum(d => d.DaySubtotal);
        // Ongkir sesuai aturan metode pemesanan & jumlah hari kirim (lihat FulfillmentFees)
        var feeResult = FulfillmentFees.Compute(settings.GetFeeRule(fulfillmentType), days.Count);
        var deliveryFee = feeResult.Fee;

        // 4b. Voucher (opsional): syarat & potongan dicek ulang di server. Saat ini pesanan selalu QRIS.
        ResolvedVoucher? voucher = null;
        if (!string.IsNullOrWhiteSpace(request.VoucherCode))
        {
            voucher = await voucherService.ResolveForOrderAsync(
                userId, request.VoucherCode, subtotal, OrderPaymentMethods.Qris, nowUtc);
        }

        var discountAmount = voucher?.DiscountAmount ?? 0m;
        // 4c. Biaya layanan & pajak dari subtotal setelah diskon (lihat OrderCharges)
        var charges = OrderCharges.Compute(subtotal - discountAmount, settings);
        var grandTotal = subtotal - discountAmount + charges.ServiceFee + charges.TaxAmount + deliveryFee;

        if (grandTotal <= 0)
        {
            throw new InvalidOrderException("Total pesanan harus lebih dari Rp 0.");
        }

        if (grandTotal != decimal.Truncate(grandTotal))
        {
            throw new InvalidOrderException("Total pesanan harus bilangan bulat Rupiah.");
        }

        // 4d. Total berubah sejak customer membuka checkout (mis. admin mengubah tarif): minta konfirmasi dulu
        if (request.ExpectedGrandTotal is { } expected && expected != grandTotal)
        {
            throw new OrderPriceChangedException(expected, new OrderPriceDto(
                subtotal, discountAmount, deliveryFee, feeResult.Note, charges.ServiceFee, charges.ServiceFeePercent,
                charges.TaxAmount, charges.TaxPercent, grandTotal));
        }

        var user = await userRepository.GetByIdAsync(userId)
            ?? throw new UserNotFoundException();

        var order = new Order
        {
            Id = orderId,
            OrderNumber = GenerateOrderNumber(settings, nowUtc),
            UserId = userId,
            StatusCode = OrderStatusCodes.AwaitingPayment,
            FulfillmentType = fulfillmentType,
            AddressId = addressId,
            AddressSnapshot = addressSnapshot,
            DeliveryWilayahCode = deliveryWilayahCode,
            OutletId = outletId,
            OutletSnapshot = outletSnapshot,
            Subtotal = subtotal,
            DiscountAmount = discountAmount,
            VoucherCode = voucher?.Voucher.Code,
            DeliveryFee = deliveryFee,
            DeliveryFeeNote = feeResult.Note,
            ServiceFee = charges.ServiceFee,
            ServiceFeePercent = charges.ServiceFeePercent,
            TaxAmount = charges.TaxAmount,
            TaxPercent = charges.TaxPercent,
            GrandTotal = grandTotal,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            PaymentDueAt = nowUtc.AddMinutes(settings.PaymentTimeoutMinutes),
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };

        // 5. Simpan order, hari, item, dan riwayat status awal dalam satu transaksi
        await using (var uow = await unitOfWorkFactory.BeginAsync())
        {
            await uow.InsertOrderAsync(order);
            await uow.InsertDaysAsync(days);
            await uow.InsertItemsAsync(items);

            // Dicek ulang di dalam transaksi (voucher dikunci): kuota & 1x per pengguna tetap benar
            // walau ada klik ganda atau banyak pesanan bersamaan untuk sisa kuota terakhir.
            if (voucher != null)
            {
                var redemption = await uow.InsertVoucherRedemptionAsync(new VoucherRedemption
                {
                    Id = Guid.NewGuid(),
                    VoucherId = voucher.Voucher.Id,
                    UserId = userId,
                    OrderId = order.Id,
                    DiscountAmount = discountAmount,
                    CreatedAt = nowUtc
                });

                switch (redemption)
                {
                    case VoucherRedemptionResult.QuotaExhausted:
                        throw new VoucherNotApplicableException("Kuota voucher sudah habis.");
                    case VoucherRedemptionResult.AlreadyUsed:
                        throw new VoucherNotApplicableException("Voucher ini sudah kamu pakai.");
                }
            }

            await uow.InsertHistoryAsync(new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Scope = OrderStatusScopes.Order,
                EntityId = order.Id,
                FromCode = null,
                ToCode = OrderStatusCodes.AwaitingPayment,
                ActorUserId = userId,
                ActorRole = RoleConstants.Codes.Customer,
                CreatedAt = nowUtc
            });
            await uow.CommitAsync();
        }

        // 6. Buat transaksi QRIS di Midtrans
        QrisChargeResult charge;
        try
        {
            charge = await paymentGateway.CreateQrisChargeAsync(new QrisChargeRequest(
                OrderId: order.OrderNumber,
                GrossAmount: grandTotal,
                ExpiryMinutes: settings.PaymentTimeoutMinutes,
                CustomerName: user.FullName,
                CustomerEmail: user.Email));
        }
        catch (PaymentGatewayException)
        {
            await MarkChargeFailedAsync(order.Id, DateTime.UtcNow);
            throw;
        }

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Provider = PaymentConstants.ProviderMidtrans,
            ProviderOrderId = order.OrderNumber,
            PaymentType = PaymentConstants.PaymentTypeQris,
            TransactionId = charge.TransactionId,
            TransactionStatus = charge.TransactionStatus,
            Acquirer = charge.Acquirer,
            QrString = charge.QrString,
            QrUrl = charge.QrUrl,
            GrossAmount = grandTotal,
            ExpiresAt = charge.ExpiresAtUtc ?? order.PaymentDueAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await paymentRepository.CreateAsync(payment);

        return new CreateOrderResponse(
            OrderId: order.Id,
            OrderNumber: order.OrderNumber,
            StatusCode: order.StatusCode,
            GrandTotal: order.GrandTotal,
            PaymentDueAt: order.PaymentDueAt,
            Payment: OrderMapper.ToDto(payment, includeQr: true));
    }

    public async Task<IReadOnlyList<OrderSummaryDto>> GetMyOrdersAsync(Guid userId)
    {
        var orders = await orderRepository.GetByUserIdAsync(userId);
        return orders.Select(OrderMapper.ToSummary).ToList();
    }

    public async Task<OrderPaymentStatusDto> GetPaymentStatusAsync(Guid orderId, OrderActor actor)
    {
        var status = await orderRepository.GetPaymentStatusAsync(orderId)
            ?? throw new OrderNotFoundException();

        // Sama seperti detail: pesanan orang lain dianggap tidak ada.
        if (status.UserId != actor.UserId && !actor.IsStaff)
        {
            throw new OrderNotFoundException();
        }

        return new OrderPaymentStatusDto(
            status.OrderId,
            status.StatusCode,
            status.PaymentDueAt,
            status.PaidAt,
            status.CancelReason);
    }

    public async Task<OrderDetailDto> GetOrderDetailAsync(Guid orderId, OrderActor actor)
    {
        var order = await orderRepository.GetByIdAsync(orderId)
            ?? throw new OrderNotFoundException();

        if (order.UserId != actor.UserId && !actor.IsStaff)
        {
            throw new OrderNotFoundException();
        }

        var days = (await orderRepository.GetDaysByOrderIdAsync(order.Id)).ToList();
        var items = (await orderRepository.GetItemsByDayIdsAsync(days.Select(d => d.Id).ToList()))
            .GroupBy(i => i.OrderDayId)
            .ToDictionary(g => g.Key, g => g.Select(OrderMapper.ToDto).ToList());
        var histories = await orderRepository.GetHistoriesAsync(order.Id);
        var payment = await paymentRepository.GetByOrderIdAsync(order.Id);

        var dayDtos = days.Select(d =>
        {
            var date = OrderMapper.ToDateOnly(d.DeliveryDate);
            return new OrderDayDto(
                Id: d.Id,
                DeliveryDate: date,
                DayOfWeek: d.DayOfWeek,
                DayName: OrderSettings.GetDayName(date),
                StatusCode: d.StatusCode,
                DaySubtotal: d.DaySubtotal,
                RequiresManualRefund: d.RequiresManualRefund,
                CancelReason: d.CancelReason,
                CancelledAt: d.CancelledAt,
                Items: items.TryGetValue(d.Id, out var list) ? list : []);
        }).ToList();

        return new OrderDetailDto(
            Id: order.Id,
            OrderNumber: order.OrderNumber,
            UserId: order.UserId,
            StatusCode: order.StatusCode,
            FulfillmentType: order.FulfillmentType,
            Address: OrderMapper.DeserializeAddress(order.AddressSnapshot),
            Notes: order.Notes,
            Subtotal: order.Subtotal,
            DiscountAmount: order.DiscountAmount,
            VoucherCode: order.VoucherCode,
            DeliveryFee: order.DeliveryFee,
            GrandTotal: order.GrandTotal,
            PaymentDueAt: order.PaymentDueAt,
            PaidAt: order.PaidAt,
            CompletedAt: order.CompletedAt,
            CancelledAt: order.CancelledAt,
            CancelReason: order.CancelReason,
            CreatedAt: order.CreatedAt,
            // QR hanya relevan selama order masih menunggu pembayaran
            Payment: payment == null ? null : OrderMapper.ToDto(payment, includeQr: order.StatusCode == OrderStatusCodes.AwaitingPayment),
            Days: dayDtos,
            Histories: histories.Select(h => new OrderStatusHistoryDto(
                h.Scope, h.EntityId, h.FromCode, h.ToCode, h.ActorRole, h.Reason, h.CreatedAt)).ToList(),
            Outlet: OrderMapper.DeserializeOutlet(order.OutletSnapshot),
            ServiceFee: order.ServiceFee,
            ServiceFeePercent: order.ServiceFeePercent,
            TaxAmount: order.TaxAmount,
            TaxPercent: order.TaxPercent,
            DeliveryFeeNote: order.DeliveryFeeNote);
    }

    public async Task CancelOrderAsync(Guid orderId, OrderActor actor, string? reason)
    {
        var nowUtc = DateTime.UtcNow;
        string orderNumber;

        await using (var uow = await unitOfWorkFactory.BeginAsync())
        {
            var order = await uow.LockOrderAsync(orderId);
            if (order == null || order.UserId != actor.UserId)
            {
                throw new OrderNotFoundException();
            }

            if (order.StatusCode != OrderStatusCodes.AwaitingPayment)
            {
                throw new InvalidStatusTransitionException(
                    OrderStatusCodes.PaidOrLater.Contains(order.StatusCode)
                        ? "Pesanan yang sudah dibayar tidak dapat dibatalkan."
                        : "Pesanan ini sudah tidak aktif.");
            }

            var days = await uow.GetDaysAsync(order.Id);
            var cleanReason = string.IsNullOrWhiteSpace(reason) ? "Dibatalkan oleh customer." : reason.Trim();
            if (cleanReason.Length > 500)
            {
                cleanReason = cleanReason[..500];
            }

            await statusEngine.CloseUnpaidOrderAsync(uow, order, days, OrderStatusCodes.CancelledByCustomer, actor, cleanReason, nowUtc);
            await uow.CommitAsync();
            orderNumber = order.OrderNumber;
        }

        // Tutup QR di Midtrans agar tidak bisa dibayar lagi (best effort).
        // Jika tetap terbayar, webhook menandai payment needs_review untuk refund manual.
        await paymentGateway.TryExpireTransactionAsync(orderNumber);
    }

    private async Task MarkChargeFailedAsync(Guid orderId, DateTime nowUtc)
    {
        await using var uow = await unitOfWorkFactory.BeginAsync();
        var order = await uow.LockOrderAsync(orderId);
        if (order == null || order.StatusCode != OrderStatusCodes.AwaitingPayment)
        {
            return;
        }

        var days = await uow.GetDaysAsync(order.Id);
        await statusEngine.CloseUnpaidOrderAsync(uow, order, days, OrderStatusCodes.PaymentFailed,
            OrderActor.System(order.UserId), "Gagal membuat transaksi QRIS.", nowUtc);
        await uow.CommitAsync();
    }

    /// <summary>Format: BBX-YYYYMMDD-XXXXXX (tanggal lokal + 6 karakter acak tanpa huruf/angka ambigu).</summary>
    private static string GenerateOrderNumber(OrderSettings settings, DateTime nowUtc)
    {
        var localDate = settings.GetLocalToday(nowUtc);
        Span<char> suffix = stackalloc char[6];
        for (var i = 0; i < suffix.Length; i++)
        {
            suffix[i] = OrderNumberAlphabet[RandomNumberGenerator.GetInt32(OrderNumberAlphabet.Length)];
        }

        return $"BBX-{localDate:yyyyMMdd}-{new string(suffix)}";
    }
}
