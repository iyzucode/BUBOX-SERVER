using Bubox.Domain.Entities;

namespace Bubox.Domain.Interfaces;

public interface IOutletRepository
{
    /// <summary>Semua outlet (admin), urut sort_order lalu nama.</summary>
    Task<IEnumerable<Outlet>> GetAllAsync();

    /// <summary>Outlet aktif untuk pilihan customer, urut sort_order lalu nama.</summary>
    Task<IEnumerable<Outlet>> GetActiveAsync();

    Task<Outlet?> GetByIdAsync(Guid id);

    Task<bool> NameExistsAsync(string name, Guid? excludeId = null);

    /// <summary>Outlet pada pesanan PICKUP terakhir pengguna (null bila belum pernah).</summary>
    Task<Guid?> GetLastUsedOutletIdAsync(Guid userId);

    /// <summary>sort_order untuk outlet baru (paling bawah).</summary>
    Task<int> GetNextSortOrderAsync();

    Task CreateAsync(Outlet outlet);
    Task UpdateAsync(Outlet outlet);
    Task SetActiveAsync(Guid id, bool isActive, Guid updatedBy, DateTime nowUtc);

    /// <summary>Hapus outlet bila belum pernah dipakai pesanan. False bila masih dipakai / tidak ada.</summary>
    Task<bool> DeleteIfUnusedAsync(Guid id);
}
