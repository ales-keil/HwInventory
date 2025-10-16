using HWInventory.Domain.Enums;

namespace HWInventory.Domain.Entities;

public class ImportJob : AuditableEntity
{
    public ImportScope Scope { get; set; }
    public ImportFormat Format { get; set; }
    public ImportJobStatus JobStatus { get; set; } = ImportJobStatus.Pending;
    public ImportConflictStrategy ConflictStrategy { get; set; } = ImportConflictStrategy.Skip;
    public bool DryRun { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFilePath { get; set; } = string.Empty;
    public string? MappingJson { get; set; }
    public string? ResultLog { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? FailureReason { get; set; }
    public long? ProcessedRows { get; set; }
    public long? CreatedRows { get; set; }
    public long? UpdatedRows { get; set; }
    public long? SkippedRows { get; set; }
    public bool SendEmail { get; set; }
    public string? EmailRecipients { get; set; }
}
