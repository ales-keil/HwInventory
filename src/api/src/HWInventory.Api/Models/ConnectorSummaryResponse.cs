using System;

namespace HWInventory.Api.Models;

public record ConnectorSummaryResponse(
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
