namespace Bubox.Application.DTOs.Biodata;

public record AddressDto(
    Guid Id,
    Guid UserId,
    string Label,
    string RecipientName,
    string PhoneNumber,
    string FullAddress,
    string Subdistrict,
    string City,
    string Province,
    string PostalCode,
    string? Notes,
    bool IsPrimary,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateAddressRequest(
    string Label,
    string RecipientName,
    string PhoneNumber,
    string FullAddress,
    string Subdistrict,
    string City,
    string Province,
    string PostalCode,
    string? Notes,
    bool IsPrimary = false
);

public record UpdateAddressRequest(
    string Label,
    string RecipientName,
    string PhoneNumber,
    string FullAddress,
    string Subdistrict,
    string City,
    string Province,
    string PostalCode,
    string? Notes,
    bool IsPrimary = false
);
