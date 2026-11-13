namespace Bubox.Application.DTOs.Vouchers;

/// <summary>Voucher untuk pelanggan, lengkap dengan kelayakan terhadap keranjang saat ini.</summary>
public record VoucherOptionDto(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    string DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscount,
    decimal MinSubtotal,
    IReadOnlyList<string> PaymentMethods,
    DateTime EndsAt,
    bool IsEligible,
    // Alasan voucher belum bisa dipakai (null bila bisa dipakai).
    string? IneligibleReason,
    // Perkiraan potongan untuk subtotal yang dikirim (0 bila belum memenuhi syarat).
    decimal EstimatedDiscount
);

/// <summary>Cek voucher dari kode yang diketik (termasuk voucher rahasia).</summary>
public record CheckVoucherRequest(string Code, decimal Subtotal, string? PaymentMethod);

public record VoucherAdminDto(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    string DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscount,
    decimal MinSubtotal,
    IReadOnlyList<string> PaymentMethods,
    // Kuota total (null = tanpa batas)
    int? UsageLimit,
    DateTime StartsAt,
    DateTime EndsAt,
    bool IsListed,
    bool IsActive,
    // Jumlah pemakaian aktif (pesanan yang batal sebelum dibayar tidak dihitung).
    int RedemptionCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record UpsertVoucherRequest(
    string Code,
    string Title,
    string? Description,
    string DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscount,
    decimal MinSubtotal,
    List<string> PaymentMethods,
    // Kuota total pemakaian untuk semua pengguna; null = tanpa batas
    int? UsageLimit,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool IsListed,
    bool IsActive
);

public record SetVoucherActiveRequest(bool IsActive);
