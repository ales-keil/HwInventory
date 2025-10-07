namespace HWInventory.Domain.Entities;

public class AppUser : AuditableEntity
{
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsDomainAccount { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool TwoFactorRequired { get; set; }
    public string? Department { get; set; }
    public ICollection<AppUserRole> UserRoles { get; set; } = new List<AppUserRole>();
    public ICollection<DataScope> DataScopes { get; set; } = new List<DataScope>();
}
