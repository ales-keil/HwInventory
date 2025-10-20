using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HWInventory.Infrastructure.Observability;

public class SecurityMetricsSnapshotService : ISecurityMetricsSnapshotService
{
    private readonly IAppDbContext _dbContext;
    private readonly ISecurityMetricsProvider _metricsProvider;
    private readonly ISecurityAlertConfigurationStore _alertConfigurationStore;
    private readonly IEmailConnectorStore _emailConnectorStore;
    private readonly ILogger<SecurityMetricsSnapshotService> _logger;

    public SecurityMetricsSnapshotService(
        IAppDbContext dbContext,
        ISecurityMetricsProvider metricsProvider,
        ISecurityAlertConfigurationStore alertConfigurationStore,
        IEmailConnectorStore emailConnectorStore,
        ILogger<SecurityMetricsSnapshotService> logger)
    {
        _dbContext = dbContext;
        _metricsProvider = metricsProvider;
        _alertConfigurationStore = alertConfigurationStore;
        _emailConnectorStore = emailConnectorStore;
        _logger = logger;
    }

    public async Task<SecurityMetricSnapshotModel> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var metrics = await _metricsProvider.GetSnapshotAsync(cancellationToken);
        var configuration = await _alertConfigurationStore.GetAsync(cancellationToken);

        var alerts = EvaluateAlerts(metrics, configuration);

        var entity = new SecurityMetricSnapshot
        {
            CapturedAtUtc = metrics.GeneratedAtUtc,
            TotalUsers = metrics.TotalUsers,
            ActiveUsers = metrics.ActiveUsers,
            TotpEnabled = metrics.TotpEnabled,
            TotpRequired = metrics.TotpRequired,
            LockedOut = metrics.LockedOut,
            PendingPasswordResets = metrics.PendingPasswordResets,
            ActiveSessions = metrics.ActiveSessions,
            AlertsJson = alerts.Count > 0 ? JsonSerializer.Serialize(alerts) : null,
            CreatedBy = "system",
            ModifiedBy = "system"
        };

        _dbContext.SecurityMetricSnapshots.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (configuration.Enabled && alerts.Any() && configuration.NotificationEmails.Count > 0)
        {
            await SendAlertAsync(configuration, metrics, alerts, cancellationToken);
        }

        return Map(entity, alerts);
    }

    public async Task<IReadOnlyList<SecurityMetricSnapshotModel>> GetRecentAsync(int days, CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Abs(days));

        var snapshots = await _dbContext.SecurityMetricSnapshots
            .Where(x => x.CapturedAtUtc >= since)
            .OrderByDescending(x => x.CapturedAtUtc)
            .Take(90)
            .ToListAsync(cancellationToken);

        return snapshots
            .Select(s => Map(s, DeserializeAlerts(s.AlertsJson)))
            .ToList();
    }

    private static List<SecurityAlertModel> EvaluateAlerts(SecurityMetricsSnapshot metrics, SecurityAlertConfigurationModel configuration)
    {
        var alerts = new List<SecurityAlertModel>();

        if (!configuration.Enabled)
        {
            return alerts;
        }

        if (metrics.TotalUsers > 0)
        {
            var adoption = (double)metrics.TotpEnabled / metrics.TotalUsers * 100d;
            if (adoption < configuration.MinimumTwoFactorAdoptionPercentage)
            {
                alerts.Add(new SecurityAlertModel(
                    "LOW_2FA_ADOPTION",
                    $"Two-factor adoption is {adoption:F1}% which is below the configured minimum of {configuration.MinimumTwoFactorAdoptionPercentage:F1}%.",
                    "warning"));
            }
        }

        if (metrics.LockedOut >= configuration.LockedAccountThreshold)
        {
            alerts.Add(new SecurityAlertModel(
                "LOCKED_ACCOUNTS",
                $"There are {metrics.LockedOut} locked user accounts which meets or exceeds the threshold of {configuration.LockedAccountThreshold}.",
                "error"));
        }

        if (metrics.PendingPasswordResets >= configuration.PendingResetThreshold)
        {
            alerts.Add(new SecurityAlertModel(
                "PENDING_RESETS",
                $"There are {metrics.PendingPasswordResets} pending password resets which meets or exceeds the threshold of {configuration.PendingResetThreshold}.",
                "warning"));
        }

        return alerts;
    }

    private async Task SendAlertAsync(SecurityAlertConfigurationModel configuration, SecurityMetricsSnapshot metrics, IReadOnlyCollection<SecurityAlertModel> alerts, CancellationToken cancellationToken)
    {
        try
        {
            var lines = new List<string>
            {
                "Security alert summary:",
                $"Total users: {metrics.TotalUsers}",
                $"Active users: {metrics.ActiveUsers}",
                $"TOTP enabled: {metrics.TotpEnabled}",
                $"Locked accounts: {metrics.LockedOut}",
                $"Pending resets: {metrics.PendingPasswordResets}",
                $"Active sessions: {metrics.ActiveSessions}"
            };

            foreach (var alert in alerts)
            {
                lines.Add($"- [{alert.Severity.ToUpperInvariant()}] {alert.Message}");
            }

            var body = string.Join(Environment.NewLine, lines);

            await _emailConnectorStore.SendNotificationAsync(new EmailNotificationRequest(
                configuration.NotificationEmails,
                "HW Inventory – Security alert",
                body,
                false),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send security alert notification");
        }
    }

    private static SecurityMetricSnapshotModel Map(SecurityMetricSnapshot entity, IReadOnlyCollection<SecurityAlertModel> alerts)
    {
        return new SecurityMetricSnapshotModel(
            entity.CapturedAtUtc,
            entity.TotalUsers,
            entity.ActiveUsers,
            entity.TotpEnabled,
            entity.TotpRequired,
            entity.LockedOut,
            entity.PendingPasswordResets,
            entity.ActiveSessions,
            alerts);
    }

    private static IReadOnlyCollection<SecurityAlertModel> DeserializeAlerts(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<SecurityAlertModel>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<SecurityAlertModel>>(json) ?? new List<SecurityAlertModel>();
        }
        catch
        {
            return Array.Empty<SecurityAlertModel>();
        }
    }
}
