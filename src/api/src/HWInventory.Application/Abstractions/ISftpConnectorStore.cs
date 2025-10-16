using System;
using System.Threading;
using System.Threading.Tasks;

namespace HWInventory.Application.Abstractions;

public interface ISftpConnectorStore
{
    Task<SftpConnectorModel?> GetAsync(CancellationToken cancellationToken = default);
    Task<SftpConnectorModel> SaveAsync(SftpConnectorUpdate update, CancellationToken cancellationToken = default);
    Task<SftpConnectorTestResult> TestAsync(CancellationToken cancellationToken = default);
}

public record SftpConnectorModel(
    Guid Id,
    string Alias,
    bool Enabled,
    string Protocol,
    string Host,
    int Port,
    string? RemotePath,
    string? Username,
    bool UseKeyAuthentication,
    bool PassiveMode,
    bool? UseImplicitFtps,
    bool AllowUnknownHosts,
    bool HasPassword,
    bool HasPrivateKey,
    string? HealthStatus,
    DateTime? LastTestedAtUtc);

public record SftpConnectorUpdate(
    string Alias,
    bool Enabled,
    string Protocol,
    string Host,
    int Port,
    string? RemotePath,
    string? Username,
    bool UseKeyAuthentication,
    bool PassiveMode,
    bool? UseImplicitFtps,
    bool AllowUnknownHosts,
    bool RotateSecrets,
    string? Password,
    string? PrivateKey,
    string? KnownHostsFingerprint);

public record SftpConnectorTestResult(bool Success, string Message);
