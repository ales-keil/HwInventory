using HWInventory.Domain.Enums;

namespace HWInventory.Domain.Entities;

public class BackupJob : AuditableEntity
{
    public BackupScope Scope { get; set; }
    public bool IncludeDictionariesOnly => Scope == BackupScope.Dictionaries;
    public string StoragePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public bool EncryptionEnabled { get; set; }
    public string? ProtectedEncryptionSecret { get; set; }
    public bool SendEmail { get; set; }
    public string? EmailRecipients { get; set; }
    public bool IntegrityCheckEnabled { get; set; }
    public BackupJobStatus JobStatus { get; set; } = BackupJobStatus.Pending;
    public long? FileSizeBytes { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public bool? IntegrityPassed { get; set; }
    public DateTime? IntegrityCheckedAtUtc { get; set; }
    public string? ArtifactPath { get; set; }
    public string? FailureReason { get; set; }
    public bool IsAutomatic { get; set; }
}
