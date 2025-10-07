namespace HWInventory.Domain.Entities;

public class DataScope : BaseEntity
{
    public Guid AppUserId { get; set; }
    public AppUser AppUser { get; set; } = default!;
    public Guid? LocationId { get; set; }
    public string? DepartmentKey { get; set; }
    public string ScopeType { get; set; } = string.Empty;
}
