using System;
using System.Threading;
using System.Threading.Tasks;

namespace HWInventory.Application.Abstractions;

public interface IStorageConnectorStore
{
    Task<StorageConnectorModel?> GetAsync(CancellationToken cancellationToken = default);
    Task<StorageConnectorModel> SaveAsync(StorageConnectorUpdate update, CancellationToken cancellationToken = default);
    Task<StorageConnectorTestResult> TestAsync(CancellationToken cancellationToken = default);
}

public record StorageConnectorModel(
    Guid Id,
    string Alias,
    bool Enabled,
    string Type,
    string? Path,
    string? Endpoint,
    string? Bucket,
    string? Folder,
    string? Region,
    string? Username,
    string? Domain,
    string? PublicUrlBase,
    int? RetentionDays,
    bool? UseSsl,
    bool HasPassword,
    bool HasAccessKeys,
    string? HealthStatus,
    DateTime? LastTestedAtUtc);

public record StorageConnectorUpdate(
    string Alias,
    bool Enabled,
    string Type,
    string? Path,
    string? Endpoint,
    string? Bucket,
    string? Folder,
    string? Region,
    string? Username,
    string? Domain,
    string? PublicUrlBase,
    int? RetentionDays,
    bool? UseSsl,
    bool RotateSecret,
    string? Password,
    string? AccessKey,
    string? SecretKey);

public record StorageConnectorTestResult(bool Success, string Message);
