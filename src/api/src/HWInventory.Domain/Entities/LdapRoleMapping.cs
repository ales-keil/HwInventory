using HWInventory.Domain.Enums;

namespace HWInventory.Domain.Entities;

public class LdapRoleMapping : AuditableEntity
{
    public string GroupName { get; set; } = string.Empty;
    public string RoleName { get; set; } = SystemRoleNames.AppAdmin;
}
