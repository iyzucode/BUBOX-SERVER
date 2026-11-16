using Bubox.Domain.Entities;

namespace Bubox.Domain.Interfaces;

public interface IKitchenRepository
{
    /// <summary>Rilis untuk tanggal kirim ini (beserta nama perilis), null bila belum dirilis.</summary>
    Task<KitchenRelease?> GetReleaseAsync(DateTime deliveryDate);

    /// <summary>Simpan rilis; rilis ulang menimpa waktu, perilis, dan snapshot jumlah.</summary>
    Task UpsertReleaseAsync(KitchenRelease release);

    /// <summary>Batalkan rilis. False bila tanggal ini memang belum dirilis.</summary>
    Task<bool> DeleteReleaseAsync(DateTime deliveryDate);
}
