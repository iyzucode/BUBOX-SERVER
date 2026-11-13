using System.Text.RegularExpressions;
using Bubox.Application.DTOs.Vouchers;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Constants;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

public partial class VoucherService(IVoucherRepository voucherRepository) : IVoucherService
{
    [GeneratedRegex("^[A-Z0-9_-]{3,30}$")]
    private static partial Regex CodePattern();

    /// <summary>Kode disimpan & dicari dalam huruf besar tanpa spasi, jadi "promo 10" = "PROMO10".</summary>
    public static string NormalizeCode(string? code) =>
        string.Concat((code ?? string.Empty).Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

    private static string NormalizePaymentMethod(string? method)
    {
        var normalized = method?.Trim().ToUpperInvariant();
        return string.IsNullOrEmpty(normalized) ? OrderPaymentMethods.Qris : normalized;
    }

    // ==========================================
    // Pelanggan
    // ==========================================

    public async Task<IReadOnlyList<VoucherOptionDto>> GetAvailableAsync(Guid userId, decimal subtotal, string? paymentMethod)
    {
        var nowUtc = DateTime.UtcNow;
        var method = NormalizePaymentMethod(paymentMethod);
        var used = await voucherRepository.GetUsedVoucherIdsAsync(userId);
        var vouchers = await voucherRepository.GetListedActiveAsync(nowUtc);

        return vouchers
            .Where(v => !used.Contains(v.Id)) // yang sudah dipakai tidak ditampilkan lagi
            .Select(v => ToOption(v, subtotal, method, nowUtc, alreadyUsed: false))
            .OrderByDescending(o => o.IsEligible)
            .ThenByDescending(o => o.EstimatedDiscount)
            .ThenBy(o => o.EndsAt)
            .ToList();
    }

    public async Task<VoucherOptionDto> CheckAsync(Guid userId, CheckVoucherRequest request)
    {
        var code = NormalizeCode(request.Code);
        if (code.Length == 0)
        {
            throw new VoucherNotFoundException("Masukkan kode voucher.");
        }

        var voucher = await voucherRepository.GetByCodeAsync(code) ?? throw new VoucherNotFoundException();
        var used = await voucherRepository.GetUsedVoucherIdsAsync(userId);
        return ToOption(voucher, request.Subtotal, NormalizePaymentMethod(request.PaymentMethod), DateTime.UtcNow,
            used.Contains(voucher.Id));
    }

    public async Task<ResolvedVoucher> ResolveForOrderAsync(
        Guid userId, string code, decimal subtotal, string paymentMethod, DateTime nowUtc)
    {
        var normalized = NormalizeCode(code);
        var voucher = await voucherRepository.GetByCodeAsync(normalized) ?? throw new VoucherNotFoundException();
        var used = await voucherRepository.GetUsedVoucherIdsAsync(userId);

        var reason = VoucherPricing.GetIneligibleReason(voucher, subtotal, paymentMethod, nowUtc, used.Contains(voucher.Id));
        if (reason != null)
        {
            throw new VoucherNotApplicableException(reason);
        }

        var discount = VoucherPricing.CalculateDiscount(voucher, subtotal);
        if (discount <= 0)
        {
            throw new VoucherNotApplicableException("Voucher tidak memberi potongan untuk pesanan ini.");
        }

        return new ResolvedVoucher(voucher, discount);
    }

    private static VoucherOptionDto ToOption(Voucher v, decimal subtotal, string method, DateTime nowUtc, bool alreadyUsed)
    {
        var reason = VoucherPricing.GetIneligibleReason(v, subtotal, method, nowUtc, alreadyUsed);
        return new VoucherOptionDto(
            Id: v.Id,
            Code: v.Code,
            Title: v.Title,
            Description: v.Description,
            DiscountType: v.DiscountType,
            DiscountValue: v.DiscountValue,
            MaxDiscount: v.MaxDiscount,
            MinSubtotal: v.MinSubtotal,
            PaymentMethods: v.PaymentMethods,
            EndsAt: v.EndsAt,
            IsEligible: reason == null,
            IneligibleReason: reason,
            EstimatedDiscount: reason == null ? VoucherPricing.CalculateDiscount(v, subtotal) : 0m);
    }

    // ==========================================
    // Admin
    // ==========================================

    public async Task<IReadOnlyList<VoucherAdminDto>> GetAllAsync()
    {
        var vouchers = await voucherRepository.GetAllAsync();
        return vouchers.Select(ToAdminDto).ToList();
    }

    public async Task<VoucherAdminDto> CreateAsync(UpsertVoucherRequest request, Guid adminId)
    {
        var nowUtc = DateTime.UtcNow;
        var voucher = new Voucher { Id = Guid.NewGuid(), CreatedBy = adminId, CreatedAt = nowUtc };
        await ApplyRequestAsync(voucher, request, adminId, nowUtc);
        await voucherRepository.CreateAsync(voucher);
        return ToAdminDto(voucher);
    }

    public async Task<VoucherAdminDto> UpdateAsync(Guid id, UpsertVoucherRequest request, Guid adminId)
    {
        var voucher = await voucherRepository.GetByIdAsync(id) ?? throw new VoucherNotFoundException("Voucher tidak ditemukan.");
        await ApplyRequestAsync(voucher, request, adminId, DateTime.UtcNow);
        await voucherRepository.UpdateAsync(voucher);
        return ToAdminDto(voucher);
    }

    public async Task<VoucherAdminDto> SetActiveAsync(Guid id, bool isActive, Guid adminId)
    {
        var voucher = await voucherRepository.GetByIdAsync(id) ?? throw new VoucherNotFoundException("Voucher tidak ditemukan.");
        var nowUtc = DateTime.UtcNow;
        await voucherRepository.SetActiveAsync(id, isActive, adminId, nowUtc);
        voucher.IsActive = isActive;
        voucher.UpdatedBy = adminId;
        voucher.UpdatedAt = nowUtc;
        return ToAdminDto(voucher);
    }

    /// <summary>Validasi input admin lalu salin ke entity.</summary>
    private async Task ApplyRequestAsync(Voucher voucher, UpsertVoucherRequest request, Guid adminId, DateTime nowUtc)
    {
        var code = NormalizeCode(request.Code);
        if (!CodePattern().IsMatch(code))
        {
            throw new InvalidVoucherException(
                $"Kode voucher {VoucherLimits.CodeMinLength}-{VoucherLimits.CodeMaxLength} karakter: huruf, angka, '-' atau '_'.");
        }

        if (await voucherRepository.CodeExistsAsync(code, voucher.Id))
        {
            throw new VoucherCodeExistsException();
        }

        var title = request.Title?.Trim() ?? string.Empty;
        if (title.Length == 0 || title.Length > VoucherLimits.TitleMaxLength)
        {
            throw new InvalidVoucherException($"Judul voucher wajib diisi, maksimal {VoucherLimits.TitleMaxLength} karakter.");
        }

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (description?.Length > VoucherLimits.DescriptionMaxLength)
        {
            throw new InvalidVoucherException($"Deskripsi maksimal {VoucherLimits.DescriptionMaxLength} karakter.");
        }

        var type = request.DiscountType?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!VoucherDiscountTypes.All.Contains(type))
        {
            throw new InvalidVoucherException("Jenis diskon harus PERCENT atau FIXED.");
        }

        if (request.DiscountValue <= 0)
        {
            throw new InvalidVoucherException("Nilai diskon harus lebih dari 0.");
        }

        if (type == VoucherDiscountTypes.Percent && request.DiscountValue > 100)
        {
            throw new InvalidVoucherException("Diskon persen maksimal 100%.");
        }

        if (type == VoucherDiscountTypes.Fixed && request.DiscountValue != decimal.Truncate(request.DiscountValue))
        {
            throw new InvalidVoucherException("Potongan Rupiah harus bilangan bulat.");
        }

        // Batas maksimal hanya berlaku untuk diskon persen
        decimal? maxDiscount = type == VoucherDiscountTypes.Percent ? request.MaxDiscount : null;
        if (maxDiscount is <= 0)
        {
            throw new InvalidVoucherException("Maksimal potongan harus lebih dari 0, atau dikosongkan.");
        }

        if (request.MinSubtotal < 0 || request.MinSubtotal != decimal.Truncate(request.MinSubtotal))
        {
            throw new InvalidVoucherException("Minimal pembelian harus bilangan bulat, 0 atau lebih.");
        }

        var methods = (request.PaymentMethods ?? [])
            .Select(m => m?.Trim().ToUpperInvariant() ?? string.Empty)
            .Distinct()
            .ToArray();
        if (methods.Length == 0 || methods.Any(m => !OrderPaymentMethods.All.Contains(m)))
        {
            throw new InvalidVoucherException("Pilih minimal satu metode pembayaran (QRIS / Tunai).");
        }

        if (request.UsageLimit is <= 0)
        {
            throw new InvalidVoucherException("Kuota pemakaian harus lebih dari 0, atau dikosongkan untuk tanpa batas.");
        }

        // Kuota tidak boleh di bawah pemakaian yang sudah berjalan (pesanan aktif tetap memakai potongannya)
        if (request.UsageLimit is { } limit && limit < voucher.RedemptionCount)
        {
            throw new InvalidVoucherException(
                $"Kuota tidak boleh lebih kecil dari pemakaian saat ini ({voucher.RedemptionCount}×).");
        }

        var startsAt = request.StartsAt.UtcDateTime;
        var endsAt = request.EndsAt.UtcDateTime;
        if (endsAt <= startsAt)
        {
            throw new InvalidVoucherException("Waktu berakhir harus setelah waktu mulai.");
        }

        voucher.Code = code;
        voucher.Title = title;
        voucher.Description = description;
        voucher.DiscountType = type;
        voucher.DiscountValue = request.DiscountValue;
        voucher.MaxDiscount = maxDiscount;
        voucher.MinSubtotal = request.MinSubtotal;
        voucher.PaymentMethods = methods;
        voucher.UsageLimit = request.UsageLimit;
        voucher.StartsAt = startsAt;
        voucher.EndsAt = endsAt;
        voucher.IsListed = request.IsListed;
        voucher.IsActive = request.IsActive;
        voucher.UpdatedBy = adminId;
        voucher.UpdatedAt = nowUtc;
    }

    private static VoucherAdminDto ToAdminDto(Voucher v) => new(
        Id: v.Id,
        Code: v.Code,
        Title: v.Title,
        Description: v.Description,
        DiscountType: v.DiscountType,
        DiscountValue: v.DiscountValue,
        MaxDiscount: v.MaxDiscount,
        MinSubtotal: v.MinSubtotal,
        PaymentMethods: v.PaymentMethods,
        UsageLimit: v.UsageLimit,
        StartsAt: v.StartsAt,
        EndsAt: v.EndsAt,
        IsListed: v.IsListed,
        IsActive: v.IsActive,
        RedemptionCount: v.RedemptionCount,
        CreatedAt: v.CreatedAt,
        UpdatedAt: v.UpdatedAt);
}
