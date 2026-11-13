using Bubox.Domain.Entities;

namespace Bubox.Domain.Interfaces;

public interface IVoucherRepository
{
    /// <summary>Semua voucher (admin), terbaru dulu, beserta jumlah pemakaian aktif.</summary>
    Task<IEnumerable<Voucher>> GetAllAsync();

    Task<Voucher?> GetByIdAsync(Guid id);

    /// <summary>Cari berdasarkan kode (huruf besar, tanpa spasi).</summary>
    Task<Voucher?> GetByCodeAsync(string code);

    /// <summary>Voucher aktif yang tampil di daftar dan sedang berlaku pada <paramref name="nowUtc"/>.</summary>
    Task<IEnumerable<Voucher>> GetListedActiveAsync(DateTime nowUtc);

    /// <summary>Id voucher yang sedang dipakai pengguna (pemakaian belum dilepas).</summary>
    Task<IReadOnlySet<Guid>> GetUsedVoucherIdsAsync(Guid userId);

    Task<bool> CodeExistsAsync(string code, Guid? excludeId = null);

    Task CreateAsync(Voucher voucher);
    Task UpdateAsync(Voucher voucher);
    Task SetActiveAsync(Guid id, bool isActive, Guid updatedBy, DateTime nowUtc);
}
