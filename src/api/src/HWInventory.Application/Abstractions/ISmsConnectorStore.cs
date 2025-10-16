using System;
using System.Threading;
using System.Threading.Tasks;

namespace HWInventory.Application.Abstractions;

public interface ISmsConnectorStore
{
    Task<SmsConnectorModel?> GetAsync(CancellationToken cancellationToken = default);
    Task<SmsConnectorModel> SaveAsync(SmsConnectorUpdate update, CancellationToken cancellationToken = default);
}

public record SmsConnectorModel(
    Guid? Id,
    string Alias,
    bool Enabled,
    string Endpoint,
    string? Sender,
    string? Region,
    bool HasSecret,
    string? HealthStatus,
    DateTime? LastTestedAtUtc);

public record SmsConnectorUpdate(
    string Alias,
    bool Enabled,
    string Endpoint,
    string? Sender,
    string? Region,
    string? Secret,
    bool RotateSecret);
