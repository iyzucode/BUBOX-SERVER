namespace Bubox.Application.DTOs.General;

public record ProvinceDto(string Code, string Name);
public record CityDto(string Code, string Name);
public record DistrictDto(string Code, string Name);
public record VillageDto(string Code, string Name, string? PostalCode);
