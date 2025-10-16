using System;
using System.Threading;
using System.Threading.Tasks;

namespace HWInventory.Application.Abstractions;

public interface IBackupConfigurationStore
{
    Task<BackupScheduleModel> GetAsync(CancellationToken cancellationToken = default);
    Task<BackupScheduleModel> SaveAsync(BackupScheduleUpdate update, CancellationToken cancellationToken = default);
    Task<BackupScheduleTriggerResult> TryMarkTriggeredAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}

public record BackupScheduleModel(
    bool Enabled,
    string Frequency,
    int? DayOfWeek,
    int? DayOfMonth,
    TimeSpan ExecutionTimeUtc,
    string Scope,
    string StoragePath,
    bool EncryptionEnabled,
    bool HasStoredPassword,
    bool SendEmail,
    string? EmailRecipients,
    bool IntegrityCheckEnabled,
    DateTime? LastRunAtUtc,
    DateTime? NextRunAtUtc);

public record BackupScheduleUpdate(
    bool Enabled,
    string Frequency,
    int? DayOfWeek,
    int? DayOfMonth,
    TimeSpan ExecutionTimeUtc,
    string Scope,
    string StoragePath,
    bool EncryptionEnabled,
    string? Password,
    bool RotatePassword,
    bool SendEmail,
    string? EmailRecipients,
    bool IntegrityCheckEnabled);

public record BackupScheduleTriggerResult(bool Triggered, BackupScheduleModel? Schedule, string? ProtectedSecret);
