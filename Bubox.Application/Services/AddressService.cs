using Bubox.Application.DTOs.Biodata;
using Bubox.Application.Exceptions;
using Bubox.Application.Interfaces;
using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

public class AddressService(
    IUserAddressRepository addressRepository,
    WilayahCodeResolver wilayahCodeResolver,
    DeliveryCoverageService coverageService) : IAddressService
{
    public async Task<IEnumerable<AddressDto>> GetAddressesAsync(Guid userId)
    {
        var addresses = await addressRepository.GetByUserIdAsync(userId);
        var result = new List<AddressDto>();
        foreach (var address in addresses)
        {
            result.Add(await MapWithCoverageAsync(address));
        }
        return result;
    }

    /// <summary>Lengkapi kode wilayah alamat lama bila perlu, lalu cek jangkauan pengantarannya.</summary>
    private async Task<AddressDto> MapWithCoverageAsync(UserAddress address)
    {
        var code = await coverageService.EnsureWilayahCodeAsync(address);
        return MapToDto(address) with { IsDeliveryCovered = await coverageService.IsCoveredAsync(code) };
    }

    public async Task<AddressDto> GetDetailAddressAsync(Guid id, Guid userId)
    {
        var address = await addressRepository.GetByIdAsync(id, userId)
            ?? throw new AddressNotFoundException();

        return await MapWithCoverageAsync(address);
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
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            PlaceId = NormalizePlaceId(request.PlaceId),
            // Kelurahan dari daftar wilayah; bila tidak ada, dicocokkan dari nama provinsi/kota/kecamatan
            WilayahCode = NormalizeWilayahCode(request.WilayahCode)
                ?? await wilayahCodeResolver.ResolveAsync(request.Province, request.City, request.Subdistrict),
            IsPrimary = isPrimary,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await addressRepository.CreateAsync(address);
        return await MapWithCoverageAsync(address);
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
        address.Latitude = request.Latitude ?? address.Latitude;
        address.Longitude = request.Longitude ?? address.Longitude;
        address.PlaceId = NormalizePlaceId(request.PlaceId) ?? address.PlaceId;
        // Nama wilayah bisa berubah: cocokkan ulang bila kelurahan tidak dipilih dari daftar
        address.WilayahCode = NormalizeWilayahCode(request.WilayahCode)
            ?? await wilayahCodeResolver.ResolveAsync(address.Province, address.City, address.Subdistrict);
        address.IsPrimary = isPrimary;
        address.UpdatedAt = DateTime.UtcNow;

        await addressRepository.UpdateAsync(address);
        return await MapWithCoverageAsync(address);
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

    private static readonly System.Text.RegularExpressions.Regex VillageCodeRegex =
        new(@"^\d{2}\.\d{2}\.\d{2}\.\d{4}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Hanya terima kode kelurahan/desa berformat resmi; selain itu diabaikan.</summary>
    private static string? NormalizeWilayahCode(string? code)
    {
        var trimmed = code?.Trim();
        return !string.IsNullOrEmpty(trimmed) && VillageCodeRegex.IsMatch(trimmed) ? trimmed : null;
    }

    private static string? NormalizePlaceId(string? placeId)
    {
        var trimmed = placeId?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed[..Math.Min(trimmed.Length, 255)];
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
        address.Latitude,
        address.Longitude,
        address.PlaceId,
        address.WilayahCode,
        address.IsPrimary,
        address.CreatedAt,
        address.UpdatedAt
    );
}
