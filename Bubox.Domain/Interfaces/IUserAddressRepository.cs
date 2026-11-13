using Bubox.Domain.Entities;

namespace Bubox.Domain.Interfaces;

public interface IUserAddressRepository
{
    Task<IEnumerable<UserAddress>> GetByUserIdAsync(Guid userId);
    Task<UserAddress?> GetByIdAsync(Guid id, Guid userId);
    Task<UserAddress?> GetPrimaryAddressAsync(Guid userId);
    Task<Guid> CreateAsync(UserAddress address);
    Task UpdateAsync(UserAddress address);

    /// <summary>Isi kode wilayah alamat lama yang belum punya kode (hasil pencocokan nama wilayah).</summary>
    Task UpdateWilayahCodeAsync(Guid addressId, string wilayahCode);
    Task SetPrimaryAddressAsync(Guid addressId, Guid userId);
    Task DeleteAsync(Guid addressId, Guid userId);
    Task<int> CountByUserIdAsync(Guid userId);
}
