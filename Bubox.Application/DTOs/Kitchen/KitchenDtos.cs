namespace Bubox.Application.DTOs.Kitchen;

// ==========================================
// Permintaan
// ==========================================

public record KitchenDateRequest(DateOnly Date);

public record KitchenMarkReadyRequest(DateOnly Date, List<Guid> OrderDayIds);

// ==========================================
// Bersama
// ==========================================

public record KitchenReleaseInfoDto(DateTime ReleasedAt, string? ReleasedByName, int DayCount, int Portions);

/// <summary>Jumlah porsi satu menu (rekap masak / rekap kelompok).</summary>
public record KitchenRecapMenuDto(Guid MenuId, string MenuName, string CategoryName, int Qty);

/// <summary>Kelompok kemas: OUTLET (Ambil Sendiri) atau ZONE (Pesan Antar).</summary>
public static class KitchenGroupKinds
{
    public const string Outlet = "OUTLET";
    public const string Zone = "ZONE";
}

// ==========================================
// Validasi Dapur (admin, H-1)
// ==========================================

/// <summary>Satu hal yang perlu diperiksa admin (pesanan menunggu bayar, hari tanpa driver, bayar ditinjau).</summary>
public record KitchenIssueDto(Guid OrderId, Guid? OrderDayId, string OrderNumber, string Name, string? Detail);

public record KitchenGroupSummaryDto(string Key, string Kind, string Name, string? Subtitle, int Packages, int Portions);

public record KitchenValidationDto(
    DateOnly Date,
    KitchenReleaseInfoDto? Release,
    DateTime CutoffAt,
    bool CutoffPassed,
    /// <summary>Batas bayar pesanan terakhir yang masuk sebelum cutoff (cutoff + batas waktu bayar).</summary>
    DateTime PaymentClosesAt,
    // Memblokir rilis
    IReadOnlyList<KitchenIssueDto> AwaitingPayment,
    // Peringatan (rilis tetap bisa)
    IReadOnlyList<KitchenIssueDto> NeedsDriver,
    IReadOnlyList<KitchenIssueDto> PaymentReview,
    bool CanRelease,
    /// <summary>Alasan rilis belum bisa (kosong bila bisa).</summary>
    IReadOnlyList<string> Blockers,
    int Packages,
    int Portions,
    IReadOnlyList<KitchenRecapMenuDto> Recap,
    IReadOnlyList<KitchenGroupSummaryDto> Groups
);

// ==========================================
// Dapur (hari H)
// ==========================================

public record KitchenItemDto(string MenuName, string CategoryName, int Qty);

/// <summary>Satu paket (satu hari kirim satu pesanan).</summary>
public record KitchenPackageDto(
    Guid OrderDayId,
    string OrderNumber,
    string RecipientName,
    IReadOnlyList<KitchenItemDto> Items,
    string? Notes,
    string DayStatus,
    bool IsReady,
    /// <summary>Dibatalkan admin setelah daftar dirilis: jangan dikemas.</summary>
    bool CancelledAfterRelease
);

public record KitchenGroupDto(
    string Key,
    string Kind,
    string Name,
    string? Subtitle,
    int Packages,
    int ReadyPackages,
    int Portions,
    IReadOnlyList<KitchenRecapMenuDto> Recap,
    IReadOnlyList<KitchenPackageDto> Items
);

/// <summary>Papan dapur satu tanggal. Released = false → belum dirilis admin, tanpa data.</summary>
public record KitchenBoardDto(
    DateOnly Date,
    bool Released,
    KitchenReleaseInfoDto? Release,
    int Packages,
    int ReadyPackages,
    int Portions,
    IReadOnlyList<KitchenRecapMenuDto> Recap,
    IReadOnlyList<KitchenGroupDto> Groups
);
