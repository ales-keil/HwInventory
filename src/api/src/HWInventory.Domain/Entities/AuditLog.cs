namespace HWInventory.Domain.Entities;

public class AuditLog : BaseEntity
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public string? Roles { get; set; }
    public DateTime PerformedAtUtc { get; set; }
    public string? ChangeSummary { get; set; }
    public string? ChangedFieldsJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
