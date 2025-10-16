namespace HWInventory.Domain.Entities;

public class BackupSchedule : BaseEntity
{
    public bool Enabled { get; set; }
    public string Frequency { get; set; } = "Daily";
    public int? DayOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public TimeSpan ExecutionTimeUtc { get; set; } = TimeSpan.FromHours(1);
    public string Scope { get; set; } = "Full";
    public string StoragePath { get; set; } = string.Empty;
    public bool EncryptionEnabled { get; set; }
    public string? ProtectedEncryptionSecret { get; set; }
    public bool SendEmail { get; set; }
    public string? EmailRecipients { get; set; }
    public bool IntegrityCheckEnabled { get; set; }
    public DateTime? LastRunAtUtc { get; set; }
    public DateTime? NextRunAtUtc { get; set; }
}
