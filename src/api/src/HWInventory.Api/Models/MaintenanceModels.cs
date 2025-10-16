using System;
using System.ComponentModel.DataAnnotations;

namespace HWInventory.Api.Models;

public class BackupRequestDto
{
    [Required]
    public string Scope { get; set; } = "Full";

    [Required]
    public string StoragePath { get; set; } = string.Empty;

    public bool EncryptionEnabled { get; set; }

    public string? Password { get; set; }

    public string? PasswordConfirmation { get; set; }

    public bool SendEmail { get; set; }

    public string? EmailRecipients { get; set; }

    public bool IntegrityCheckEnabled { get; set; } = true;
}

public class BackupResponseDto
{
    public Guid Id { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public bool EncryptionEnabled { get; set; }
    public bool SendEmail { get; set; }
    public bool IntegrityCheckEnabled { get; set; }
    public bool IsAutomatic { get; set; }
    public string Status { get; set; } = string.Empty;
    public long? FileSizeBytes { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public bool? IntegrityPassed { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
}

public class RestoreRequestDto
{
    public string? Password { get; set; }
    public bool PerformIntegrityTest { get; set; }
}

public class IntegrityTestRequestDto
{
    public string? Password { get; set; }
}

public class BackupScheduleResponseDto
{
    public bool Enabled { get; set; }
    public string Frequency { get; set; } = "Daily";
    public int? DayOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public string ExecutionTimeUtc { get; set; } = "01:00";
    public string Scope { get; set; } = "Full";
    public string StoragePath { get; set; } = string.Empty;
    public bool EncryptionEnabled { get; set; }
    public bool HasStoredPassword { get; set; }
    public bool SendEmail { get; set; }
    public string? EmailRecipients { get; set; }
    public bool IntegrityCheckEnabled { get; set; }
    public DateTime? LastRunAtUtc { get; set; }
    public DateTime? NextRunAtUtc { get; set; }
}

public class BackupScheduleRequestDto
{
    public bool Enabled { get; set; }
    public string Frequency { get; set; } = "Daily";
    public int? DayOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public string ExecutionTimeUtc { get; set; } = "01:00";
    public string Scope { get; set; } = "Full";
    public string StoragePath { get; set; } = string.Empty;
    public bool EncryptionEnabled { get; set; }
    public string? Password { get; set; }
    public string? PasswordConfirmation { get; set; }
    public bool RotatePassword { get; set; }
    public bool SendEmail { get; set; }
    public string? EmailRecipients { get; set; }
    public bool IntegrityCheckEnabled { get; set; } = true;
}
