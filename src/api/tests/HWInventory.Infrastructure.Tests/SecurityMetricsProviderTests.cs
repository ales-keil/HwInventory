using System;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using HWInventory.Infrastructure.Observability;
using HWInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HWInventory.Infrastructure.Tests;

public class SecurityMetricsProviderTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task SnapshotReflectsUserState()
    {
        await using var context = CreateDbContext();
        var provider = new SecurityMetricsProvider(context);

        var activeUser = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "active@example.com",
            NormalizedUserName = "ACTIVE@EXAMPLE.COM",
            Email = "active@example.com",
            NormalizedEmail = "ACTIVE@EXAMPLE.COM",
            Status = EntityStatus.Active,
            TwoFactorEnabled = true,
            TwoFactorRequired = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var lockedUser = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "locked@example.com",
            NormalizedUserName = "LOCKED@EXAMPLE.COM",
            Email = "locked@example.com",
            NormalizedEmail = "LOCKED@EXAMPLE.COM",
            Status = EntityStatus.Active,
            LockoutEnd = DateTimeOffset.UtcNow.AddHours(2),
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        var disabledUser = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "disabled@example.com",
            NormalizedUserName = "DISABLED@EXAMPLE.COM",
            Email = "disabled@example.com",
            NormalizedEmail = "DISABLED@EXAMPLE.COM",
            Status = EntityStatus.Disabled,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "seed"
        };

        context.Users.AddRange(activeUser, lockedUser, disabledUser);
        context.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            AppUser = activeUser,
            Token = Guid.NewGuid(),
            DeliveryMethod = "Email",
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30),
            Status = PasswordResetStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "system"
        });
        context.UserSessions.Add(new UserSession
        {
            Id = Guid.NewGuid(),
            AppUser = activeUser,
            SessionIdentifier = Guid.NewGuid().ToString(),
            IssuedAtUtc = DateTime.UtcNow.AddHours(-1),
            LastSeenAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "system"
        });
        await context.SaveChangesAsync();

        var snapshot = await provider.GetSnapshotAsync(CancellationToken.None);

        Assert.Equal(3, snapshot.TotalUsers);
        Assert.Equal(2, snapshot.ActiveUsers);
        Assert.Equal(1, snapshot.TotpEnabled);
        Assert.Equal(1, snapshot.TotpRequired);
        Assert.Equal(1, snapshot.LockedOut);
        Assert.Equal(1, snapshot.PendingPasswordResets);
        Assert.Equal(1, snapshot.ActiveSessions);
    }

    [Fact]
    public void PrometheusFormattingProducesMetrics()
    {
        var provider = new SecurityMetricsProvider(CreateDbContext());
        var snapshot = new SecurityMetricsSnapshot(5, 4, 3, 2, 1, 1, 2, DateTime.UtcNow);

        var output = provider.FormatPrometheus(snapshot);

        Assert.Contains("hwinventory_security_users_total", output);
        Assert.Contains("hwinventory_security_sessions_active", output);
        Assert.Contains("hwinventory_security_snapshot_timestamp_seconds", output);
    }
}
