using Bubox.Domain.Entities;

namespace Bubox.Domain.Interfaces;

public interface IMenuRepository
{
    Task<Guid> CreateAsync(Menu menu);
    Task UpdateAsync(Menu menu);
    Task SoftDeleteAsync(Guid id);
    Task ToggleStatusAsync(Guid id, bool isActive);
    Task<Menu?> GetByIdAsync(Guid id);

    /// <summary>Isi kolom image_url (data URL / URL) termasuk menu yang sudah dihapus; null bila kosong.</summary>
    Task<string?> GetImageAsync(Guid id);
    Task<IEnumerable<Menu>> GetAllMasterMenusAsync(Guid? categoryId = null, string? search = null, bool includeInactive = false);
    Task<bool> ExistsByNameInCategoryAsync(Guid categoryId, string name, Guid? excludeId = null);
}
