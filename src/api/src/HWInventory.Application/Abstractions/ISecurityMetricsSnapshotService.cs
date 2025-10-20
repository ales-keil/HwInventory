using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HWInventory.Application.Abstractions;

public interface ISecurityMetricsSnapshotService
{
    Task<SecurityMetricSnapshotModel> CaptureAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SecurityMetricSnapshotModel>> GetRecentAsync(int days, CancellationToken cancellationToken = default);
}

public record SecurityMetricSnapshotModel(
    DateTime CapturedAtUtc,
    int TotalUsers,
    int ActiveUsers,
    int TotpEnabled,
    int TotpRequired,
    int LockedOut,
    int PendingPasswordResets,
    int ActiveSessions,
    IReadOnlyCollection<SecurityAlertModel> Alerts);

public record SecurityAlertModel(string Code, string Message, string Severity);
