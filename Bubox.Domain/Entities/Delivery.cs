namespace Bubox.Domain.Entities;

/// <summary>Satu node master wilayah (rf_wilayah).</summary>
public class WilayahNode
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Hanya terisi pada pencarian kode pos.</summary>
    public string? PostalCode { get; set; }
}

/// <summary>Aturan cakupan pengantaran untuk satu wilayah (delivery_coverage_areas).</summary>
public class CoverageRule
{
    public string WilayahCode { get; set; } = string.Empty;
    public bool IsCovered { get; set; }
}

/// <summary>Pemetaan wilayah → zona (delivery_zone_areas) beserta info zonanya.</summary>
public class ZoneAreaRule
{
    public string WilayahCode { get; set; } = string.Empty;
    public Guid ZoneId { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public bool ZoneIsActive { get; set; }
}

/// <summary>Jumlah aturan di bawah (keturunan) satu wilayah, untuk penanda di pohon admin.</summary>
public class DescendantRuleCount
{
    public string Code { get; set; } = string.Empty;
    public int CoverageRules { get; set; }
    public int ZoneRules { get; set; }
}

/// <summary>Zona pengantaran ("driver wilayah").</summary>
public class DeliveryZone
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Hanya terisi pada query daftar
    public int AreaCount { get; set; }
    public Guid? DriverUserId { get; set; }
    public string? DriverName { get; set; }
    public string? DriverEmail { get; set; }
}

public class ZoneDriverHistoryRow
{
    public Guid DriverUserId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public DateTime AssignedAt { get; set; }
    public DateTime? UnassignedAt { get; set; }
}

/// <summary>User ber-peran Driver beserta zona yang sedang dipegang.</summary>
public class DriverRow
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? PhoneNumber { get; set; }
    /// <summary>Nama zona yang dipegang, dipisah koma (null bila belum memegang zona).</summary>
    public string? ZoneNames { get; set; }
}

public class UserSearchRow
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Username { get; set; }
    public bool IsDriver { get; set; }
}

/// <summary>Penugasan efektif satu hari kirim (view v_order_day_delivery_assignments).</summary>
public class DayAssignment
{
    public Guid OrderDayId { get; set; }
    public Guid? ZoneId { get; set; }
    public string? ZoneName { get; set; }
    public Guid? DriverUserId { get; set; }
    public string? DriverName { get; set; }
    /// <summary>DISPATCHED | MANUAL | ZONE | UNASSIGNED</summary>
    public string AssignmentSource { get; set; } = "UNASSIGNED";
}
