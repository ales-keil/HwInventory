using System.Collections.Generic;
using HWInventory.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace HWInventory.Domain.Entities;

public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsDomainAccount { get; set; }
    public bool TwoFactorRequired { get; set; }
    public string? Department { get; set; }
    public string? AuthenticatorKey { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Active;
    public ICollection<DataScope> DataScopes { get; set; } = new List<DataScope>();
}
