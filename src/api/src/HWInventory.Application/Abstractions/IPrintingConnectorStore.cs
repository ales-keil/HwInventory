using System;
using System.Threading;
using System.Threading.Tasks;

namespace HWInventory.Application.Abstractions;

public interface IPrintingConnectorStore
{
    Task<PrintingConnectorModel?> GetAsync(CancellationToken cancellationToken = default);
    Task<PrintingConnectorModel> SaveAsync(PrintingConnectorUpdate update, CancellationToken cancellationToken = default);
    Task<PrintingConnectorTestResult> TestAsync(CancellationToken cancellationToken = default);
}

public record PrintingConnectorModel(
    Guid Id,
    string Alias,
    bool Enabled,
    string Host,
    int Port,
    string QueueType,
    int? TimeoutSeconds,
    int? RetryCount,
    bool HasSecret,
    string? HealthStatus,
    DateTime? LastTestedAtUtc);

public record PrintingConnectorUpdate(
    string Alias,
    bool Enabled,
    string Host,
    int Port,
    string QueueType,
    int? TimeoutSeconds,
    int? RetryCount,
    bool RotateSecret,
    string? SharedSecret);

public record PrintingConnectorTestResult(bool Success, string Message);
