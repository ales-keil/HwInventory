namespace HWInventory.Domain.Entities;

public class AppUserRole
{
    public Guid AppUserId { get; set; }
    public AppUser AppUser { get; set; } = default!;
    public Guid AppRoleId { get; set; }
    public AppRole AppRole { get; set; } = default!;
}
