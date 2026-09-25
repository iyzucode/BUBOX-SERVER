using Bubox.Application.DTOs.Biodata;

namespace Bubox.Application.Interfaces;

public interface IAddressService
{
    Task<IEnumerable<AddressDto>> GetAddressesAsync(Guid userId);
    Task<AddressDto> GetDetailAddressAsync(Guid id, Guid userId);
    Task<AddressDto> SaveAddressAsync(Guid userId, CreateAddressRequest request);
    Task<AddressDto> UpdateAddressAsync(Guid id, Guid userId, UpdateAddressRequest request);
    Task SetPrimaryAddressAsync(Guid id, Guid userId);
    Task DeleteAddressAsync(Guid id, Guid userId);
}
