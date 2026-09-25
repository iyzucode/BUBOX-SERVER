namespace Bubox.Domain.Entities;

public class Menu
{
    public Guid Id { get; set; }
    public int DayOfWeek { get; set; } // 1: Senin, ..., 7: Minggu
    public string DayName { get; set; } = string.Empty;
    public string MenuType { get; set; } = string.Empty; // UTAMA, SEKUNDER
    public string Category { get; set; } = string.Empty; // Bubur, Nasi Tim, Sup, Snack, Pelengkap
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Ingredients { get; set; }
    public string? NutritionInfo { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
