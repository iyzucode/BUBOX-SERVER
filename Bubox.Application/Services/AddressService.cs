using Bubox.Application.DTOs.Biodata;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

public class AddressService(IUserAddressRepository addressRepository) : IAddressService
{
    public async Task<IEnumerable<AddressDto>> GetAddressesAsync(Guid userId)
    {
        var addresses = await addressRepository.GetByUserIdAsync(userId);
        return addresses.Select(MapToDto);
    }

    public async Task<AddressDto> GetDetailAddressAsync(Guid id, Guid userId)
    {
        var address = await addressRepository.GetByIdAsync(id, userId)
            ?? throw new AddressNotFoundException();

        return MapToDto(address);
    }

    public async Task<AddressDto> SaveAddressAsync(Guid userId, CreateAddressRequest request)
    {
        var addressCount = await addressRepository.CountByUserIdAsync(userId);
        var isPrimary = addressCount == 0 || request.IsPrimary;

        var address = new UserAddress
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Label = request.Label.Trim(),
            RecipientName = request.RecipientName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            FullAddress = request.FullAddress.Trim(),
            Subdistrict = request.Subdistrict.Trim(),
            City = request.City.Trim(),
            Province = request.Province.Trim(),
            PostalCode = request.PostalCode.Trim(),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            IsPrimary = isPrimary,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await addressRepository.CreateAsync(address);
        return MapToDto(address);
    }

    public async Task<AddressDto> UpdateAddressAsync(Guid id, Guid userId, UpdateAddressRequest request)
    {
        var address = await addressRepository.GetByIdAsync(id, userId)
            ?? throw new AddressNotFoundException();

        var isPrimary = address.IsPrimary || request.IsPrimary;

        address.Label = request.Label.Trim();
        address.RecipientName = request.RecipientName.Trim();
        address.PhoneNumber = request.PhoneNumber.Trim();
        address.FullAddress = request.FullAddress.Trim();
        address.Subdistrict = request.Subdistrict.Trim();
        address.City = request.City.Trim();
        address.Province = request.Province.Trim();
        address.PostalCode = request.PostalCode.Trim();
        address.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        address.IsPrimary = isPrimary;
        address.UpdatedAt = DateTime.UtcNow;

        await addressRepository.UpdateAsync(address);
        return MapToDto(address);
    }

    public async Task SetPrimaryAddressAsync(Guid id, Guid userId)
    {
        var address = await addressRepository.GetByIdAsync(id, userId)
            ?? throw new AddressNotFoundException();

        if (address.IsPrimary)
        {
            return;
        }

        await addressRepository.SetPrimaryAddressAsync(id, userId);
    }

    public async Task DeleteAddressAsync(Guid id, Guid userId)
    {
        var address = await addressRepository.GetByIdAsync(id, userId)
            ?? throw new AddressNotFoundException();

        var count = await addressRepository.CountByUserIdAsync(userId);
        if (address.IsPrimary && count > 1)
        {
            throw new CannotDeletePrimaryAddressException();
        }

        await addressRepository.DeleteAsync(id, userId);
    }

    private static AddressDto MapToDto(UserAddress address) => new(
        address.Id,
        address.UserId,
        address.Label,
        address.RecipientName,
        address.PhoneNumber,
        address.FullAddress,
        address.Subdistrict,
        address.City,
        address.Province,
        address.PostalCode,
        address.Notes,
        address.IsPrimary,
        address.CreatedAt,
        address.UpdatedAt
    );
}
