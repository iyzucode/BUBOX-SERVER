using Bubox.Application.DTOs.Vouchers;
using Bubox.Domain.Entities;

namespace Bubox.Application.Interfaces;

/// <summary>Voucher yang sudah lolos semua syarat untuk satu pesanan.</summary>
public record ResolvedVoucher(Voucher Voucher, decimal DiscountAmount);

public interface IVoucherService
{
    /// <summary>Voucher yang tampil di daftar & sedang berlaku, belum dipakai pengguna, beserta kelayakannya.</summary>
    Task<IReadOnlyList<VoucherOptionDto>> GetAvailableAsync(Guid userId, decimal subtotal, string? paymentMethod);

    /// <summary>Cek kode yang diketik pengguna (voucher rahasia juga bisa).</summary>
    Task<VoucherOptionDto> CheckAsync(Guid userId, CheckVoucherRequest request);

    /// <summary>Dipakai CreateOrder: lempar <c>VoucherNotApplicableException</c> bila voucher tidak bisa dipakai.</summary>
    Task<ResolvedVoucher> ResolveForOrderAsync(Guid userId, string code, decimal subtotal, string paymentMethod, DateTime nowUtc);

    // Admin
    Task<IReadOnlyList<VoucherAdminDto>> GetAllAsync();
    Task<VoucherAdminDto> CreateAsync(UpsertVoucherRequest request, Guid adminId);
    Task<VoucherAdminDto> UpdateAsync(Guid id, UpsertVoucherRequest request, Guid adminId);
    Task<VoucherAdminDto> SetActiveAsync(Guid id, bool isActive, Guid adminId);
}
