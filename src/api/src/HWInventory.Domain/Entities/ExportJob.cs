using HWInventory.Domain.Enums;

namespace HWInventory.Domain.Entities;

public class ExportJob : AuditableEntity
{
    public ExportScope Scope { get; set; }
    public ExportFormat Format { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? FilterJson { get; set; }
    public ExportJobStatus JobStatus { get; set; } = ExportJobStatus.Pending;
    public long? FileSizeBytes { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? FailureReason { get; set; }
    public string? ArtifactPath { get; set; }
    public bool SendEmail { get; set; }
    public string? EmailRecipients { get; set; }
}
