using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Domain.Enums;

namespace HWInventory.Application.Abstractions;

public interface IBackupService
{
    Task<BackupJobModel> QueueBackupAsync(BackupRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackupJobModel>> ListHistoryAsync(int page, int size, CancellationToken cancellationToken = default);
    Task<BackupJobModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RestoreResult> RestoreAsync(Guid jobId, RestoreRequest request, CancellationToken cancellationToken = default);
    Task<IntegrityTestResult> TestIntegrityAsync(Guid jobId, string? password, CancellationToken cancellationToken = default);
    Task QueueScheduledBackupIfDueAsync(CancellationToken cancellationToken = default);
    Task ProcessPendingJobsAsync(CancellationToken cancellationToken = default);
}

public record BackupRequest(
    BackupScope Scope,
    string StoragePath,
    bool EncryptionEnabled,
    string? Password,
    string? ProtectedSecret,
    bool SendEmail,
    string? EmailRecipients,
    bool IntegrityCheckEnabled,
    bool IsAutomatic);

public record RestoreRequest(string? Password, bool PerformIntegrityTest);

public record BackupJobModel(
    Guid Id,
    string Scope,
    string FileName,
    string StoragePath,
    bool EncryptionEnabled,
    bool SendEmail,
    bool IntegrityCheckEnabled,
    bool IsAutomatic,
    string Status,
    long? FileSizeBytes,
    DateTime? CompletedAtUtc,
    bool? IntegrityPassed,
    DateTime CreatedAtUtc,
    string CreatedBy,
    string? FailureReason);

public record RestoreResult(bool Success, string Message);

public record IntegrityTestResult(bool Success, string Message, bool? Passed);
