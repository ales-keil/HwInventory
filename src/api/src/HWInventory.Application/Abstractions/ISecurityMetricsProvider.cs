using System;
using System.Threading;
using System.Threading.Tasks;

namespace HWInventory.Application.Abstractions;

public interface ISecurityMetricsProvider
{
    Task<SecurityMetricsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    string FormatPrometheus(SecurityMetricsSnapshot snapshot);
}

public sealed record SecurityMetricsSnapshot(
    int TotalUsers,
    int ActiveUsers,
    int TotpEnabled,
    int TotpRequired,
    int LockedOut,
    int PendingPasswordResets,
    int ActiveSessions,
    DateTime GeneratedAtUtc);
