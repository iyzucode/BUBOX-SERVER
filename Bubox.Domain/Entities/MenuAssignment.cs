namespace Bubox.Domain.Entities;

public class MenuAssignment
{
    public Guid Id { get; set; }
    public Guid MenuId { get; set; }
    public int DayOfWeek { get; set; } // 1: Senin, ..., 7: Minggu
    public string DayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
