using HWInventory.Domain.Enums;

namespace HWInventory.Domain.Entities;

public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }
    public EntityStatus Status { get; set; } = EntityStatus.Active;
}
