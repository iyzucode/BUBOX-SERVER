using Bubox.Domain.Entities;
using Bubox.Domain.Interfaces;

namespace Bubox.Application.Services;

/// <summary>Cek jangkauan pengantaran untuk alamat pelanggan & pesanan.</summary>
public class DeliveryCoverageService(
    IDeliveryRepository deliveryRepository,
    IUserAddressRepository addressRepository,
    WilayahCodeResolver wilayahCodeResolver)
{
    /// <summary>
    /// Pastikan alamat punya kode wilayah. Alamat lama tanpa kode dicocokkan dari nama wilayahnya,
    /// lalu hasilnya disimpan supaya tidak dicari ulang. Null bila wilayah tidak dikenali sama sekali.
    /// </summary>
    public async Task<string?> EnsureWilayahCodeAsync(UserAddress address)
    {
        if (!string.IsNullOrEmpty(address.WilayahCode)) return address.WilayahCode;

        var code = await wilayahCodeResolver.ResolveAsync(address.Province, address.City, address.Subdistrict);
        if (code != null)
        {
            await addressRepository.UpdateWilayahCodeAsync(address.Id, code);
            address.WilayahCode = code;
        }

        return code;
    }

    /// <summary>Terjangkau bila aturan paling spesifik untuk kode ini menyatakan terjangkau. Tanpa aturan = tidak.</summary>
    public async Task<bool> IsCoveredAsync(string? wilayahCode)
    {
        if (string.IsNullOrEmpty(wilayahCode)) return false;
        var rule = await deliveryRepository.GetEffectiveCoverageAsync(wilayahCode);
        return rule?.IsCovered ?? false;
    }
}
