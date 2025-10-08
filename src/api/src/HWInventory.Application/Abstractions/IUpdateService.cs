using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HWInventory.Application.Abstractions;

public interface IUpdateService
{
    Task<UpdatePackageModel> QueuePackageAsync(UpdatePackageRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UpdatePackageModel>> ListHistoryAsync(int page, int size, CancellationToken cancellationToken = default);
    Task<UpdatePackageModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<byte[]?> GetLogAsync(Guid id, CancellationToken cancellationToken = default);
    Task ProcessPendingPackagesAsync(CancellationToken cancellationToken = default);
}

public record UpdatePackageRequest(
    string FileName,
    string StoragePath,
    string ContentBase64,
    string? Version,
    bool PreserveDatabaseConfiguration,
    bool CreateBackupBeforeInstall,
    string? BackupStoragePath,
    bool PerformIntegrityCheck,
    bool ConfirmedBackupAvailable,
    string? Notes);

public record UpdatePackageModel(
    Guid Id,
    string Version,
    string FileName,
    string Status,
    string Sha256,
    string StoragePath,
    string StagingPath,
    bool PreserveDatabaseConfiguration,
    bool CreateBackupBeforeInstall,
    bool PerformIntegrityCheck,
    bool ConfirmedBackupAvailable,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string CreatedBy,
    string? FailureReason,
    string? ManifestJson,
    string? LogPath);
