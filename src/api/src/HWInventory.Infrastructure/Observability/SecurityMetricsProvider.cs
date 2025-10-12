using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Infrastructure.Observability;

public class SecurityMetricsProvider : ISecurityMetricsProvider
{
    private static readonly TimeSpan ActiveSessionWindow = TimeSpan.FromDays(7);
    private readonly AppDbContext _dbContext;

    public SecurityMetricsProvider(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SecurityMetricsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var lockoutThreshold = DateTimeOffset.UtcNow;
        var activeSince = nowUtc - ActiveSessionWindow;

        var totalUsers = await _dbContext.Users.CountAsync(cancellationToken);
        var activeUsers = await _dbContext.Users.CountAsync(u => u.Status == EntityStatus.Active, cancellationToken);
        var totpEnabled = await _dbContext.Users.CountAsync(u => u.TwoFactorEnabled, cancellationToken);
        var totpRequired = await _dbContext.Users.CountAsync(u => u.TwoFactorRequired, cancellationToken);
        var lockedOut = await _dbContext.Users.CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > lockoutThreshold, cancellationToken);
        var pendingPasswordResets = await _dbContext.PasswordResetTokens.CountAsync(
            t => t.Status == PasswordResetStatus.Pending && t.ExpiresAtUtc > nowUtc,
            cancellationToken);
        var activeSessions = await _dbContext.UserSessions.CountAsync(
            s => !s.IsRevoked && s.LastSeenAtUtc >= activeSince,
            cancellationToken);

        return new SecurityMetricsSnapshot(
            totalUsers,
            activeUsers,
            totpEnabled,
            totpRequired,
            lockedOut,
            pendingPasswordResets,
            activeSessions,
            nowUtc);
    }

    public string FormatPrometheus(SecurityMetricsSnapshot snapshot)
    {
        var sb = new StringBuilder();
        var culture = CultureInfo.InvariantCulture;

        sb.AppendLine("# HELP hwinventory_security_users_total Total registered users.");
        sb.AppendLine("# TYPE hwinventory_security_users_total gauge");
        sb.AppendLine($"hwinventory_security_users_total {snapshot.TotalUsers.ToString(culture)}");

        sb.AppendLine("# HELP hwinventory_security_users_active Active users (status = Active).");
        sb.AppendLine("# TYPE hwinventory_security_users_active gauge");
        sb.AppendLine($"hwinventory_security_users_active {snapshot.ActiveUsers.ToString(culture)}");

        sb.AppendLine("# HELP hwinventory_security_users_totp_enabled Users enrolled in TOTP 2FA.");
        sb.AppendLine("# TYPE hwinventory_security_users_totp_enabled gauge");
        sb.AppendLine($"hwinventory_security_users_totp_enabled {snapshot.TotpEnabled.ToString(culture)}");

        sb.AppendLine("# HELP hwinventory_security_users_totp_required Users required to use 2FA.");
        sb.AppendLine("# TYPE hwinventory_security_users_totp_required gauge");
        sb.AppendLine($"hwinventory_security_users_totp_required {snapshot.TotpRequired.ToString(culture)}");

        sb.AppendLine("# HELP hwinventory_security_users_locked_out Users currently locked out.");
        sb.AppendLine("# TYPE hwinventory_security_users_locked_out gauge");
        sb.AppendLine($"hwinventory_security_users_locked_out {snapshot.LockedOut.ToString(culture)}");

        sb.AppendLine("# HELP hwinventory_security_password_resets_pending Pending password reset requests.");
        sb.AppendLine("# TYPE hwinventory_security_password_resets_pending gauge");
        sb.AppendLine($"hwinventory_security_password_resets_pending {snapshot.PendingPasswordResets.ToString(culture)}");

        sb.AppendLine("# HELP hwinventory_security_sessions_active Active user sessions observed within the trailing window.");
        sb.AppendLine("# TYPE hwinventory_security_sessions_active gauge");
        sb.AppendLine($"hwinventory_security_sessions_active {snapshot.ActiveSessions.ToString(culture)}");

        sb.AppendLine("# HELP hwinventory_security_snapshot_timestamp_seconds Timestamp of the metrics snapshot (UTC).");
        sb.AppendLine("# TYPE hwinventory_security_snapshot_timestamp_seconds gauge");
        sb.AppendLine($"hwinventory_security_snapshot_timestamp_seconds {new DateTimeOffset(snapshot.GeneratedAtUtc).ToUnixTimeSeconds().ToString(culture)}");

        return sb.ToString();
    }
}
