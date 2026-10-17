using Bubox.Domain.Entities;

namespace Bubox.Domain.Interfaces;

public class AssignedMenuDetail
{
    public Guid AssignmentId { get; set; }
    public Guid MenuId { get; set; }
    public int DayOfWeek { get; set; }
    public string DayName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryType { get; set; } = string.Empty; // UTAMA, SEKUNDER
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Ingredients { get; set; }
    public string? NutritionInfo { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public interface IMenuAssignmentRepository
{
    Task<IEnumerable<AssignedMenuDetail>> GetByDayAsync(int dayOfWeek);
    Task<IEnumerable<AssignedMenuDetail>> GetAllWeeklyAssignmentsAsync();
    Task<bool> HasAssignmentsByMenuIdAsync(Guid menuId);
    Task<bool> ExistsAssignmentAsync(Guid menuId, int dayOfWeek);
    Task<Guid> AssignMenuAsync(MenuAssignment assignment);
    Task<int> BatchAssignMenusAsync(IEnumerable<MenuAssignment> assignments);
    Task HardDeleteAsync(Guid assignmentId);
    Task HardDeleteByMenuAndDayAsync(Guid menuId, int dayOfWeek);
}
