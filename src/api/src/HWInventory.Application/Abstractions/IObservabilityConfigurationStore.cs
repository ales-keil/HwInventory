using System.Threading;

namespace HWInventory.Application.Abstractions;

public interface IObservabilityConfigurationStore
{
    Task<ObservabilityConfigurationModel> GetAsync(CancellationToken cancellationToken = default);
    Task<ObservabilityConfigurationModel> SaveAsync(ObservabilityConfigurationUpdate update, CancellationToken cancellationToken = default);
}

public record ObservabilityConfigurationModel(
    string LogLevel,
    bool HealthEndpointEnabled,
    bool MetricsEndpointEnabled,
    bool CorrelationIdsEnabled,
    bool IncludeTraceIdentifier,
    bool OtelExporterEnabled,
    string? OtelEndpoint,
    bool HasOtelAuthToken,
    string? ResourceAttributes);

public record ObservabilityConfigurationUpdate(
    string LogLevel,
    bool HealthEndpointEnabled,
    bool MetricsEndpointEnabled,
    bool CorrelationIdsEnabled,
    bool IncludeTraceIdentifier,
    bool OtelExporterEnabled,
    string? OtelEndpoint,
    string? OtelAuthToken,
    bool RotateOtelAuthToken,
    string? ResourceAttributes);
