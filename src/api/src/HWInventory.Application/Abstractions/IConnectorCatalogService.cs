using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HWInventory.Application.Abstractions;

public interface IConnectorCatalogService
{
    Task<IReadOnlyCollection<ConnectorSummary>> GetAsync(CancellationToken cancellationToken = default);
    Task<ConnectorSummary?> ToggleAsync(Guid connectorId, bool enabled, CancellationToken cancellationToken = default);
    Task<ConnectorSummary?> RotateSecretsAsync(Guid connectorId, CancellationToken cancellationToken = default);
    Task<ConnectorTestOutcome> TestAsync(Guid connectorId, ConnectorTestRequest request, CancellationToken cancellationToken = default);
}

public record ConnectorSummary(
    Guid? Id,
    string Key,
    string Type,
    string Name,
    bool Enabled,
    string? HealthStatus,
    DateTime? LastTestedAtUtc,
    bool HasSecret,
    bool SupportsToggle,
    bool SupportsTest,
    bool SupportsRotateSecret,
    bool RequiresConfiguration,
    string? Description);

public record ConnectorTestRequest(string? Target);

public record ConnectorTestOutcome(ConnectorTestResult Result, ConnectorSummary? Summary);

public record ConnectorTestResult(bool Success, string Message);
