using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using HWInventory.Infrastructure.Observability;
using HWInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace HWInventory.Infrastructure.Tests;

public class SecurityMetricsSnapshotServiceTests
{
    [Fact]
    public async Task CaptureAsync_PersistsSnapshotAndSendsAlert()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new AppDbContext(options);

        var metricsProvider = new Mock<ISecurityMetricsProvider>();
        var metrics = new SecurityMetricsSnapshot(10, 8, 4, 6, 5, 3, 7, DateTime.UtcNow);
        metricsProvider.Setup(x => x.GetSnapshotAsync(It.IsAny<CancellationToken>())).ReturnsAsync(metrics);

        var alertConfig = new SecurityAlertConfigurationModel(true, 80, 3, 2, new[] { "soc@example.com" });
        var alertStore = new Mock<ISecurityAlertConfigurationStore>();
        alertStore.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(alertConfig);

        var emailStore = new Mock<IEmailConnectorStore>();
        emailStore.Setup(x => x.SendNotificationAsync(It.IsAny<EmailNotificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmailSendResult(true, "sent"));

        var service = new SecurityMetricsSnapshotService(dbContext, metricsProvider.Object, alertStore.Object, emailStore.Object, NullLogger<SecurityMetricsSnapshotService>.Instance);

        var snapshot = await service.CaptureAsync();

        Assert.Equal(metrics.TotalUsers, snapshot.TotalUsers);
        Assert.NotEmpty(snapshot.Alerts);
        Assert.Single(await dbContext.SecurityMetricSnapshots.ToListAsync());
        emailStore.Verify(x => x.SendNotificationAsync(It.IsAny<EmailNotificationRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetRecentAsync_FiltersByWindow()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new AppDbContext(options);
        dbContext.SecurityMetricSnapshots.AddRange(
            new SecurityMetricSnapshot
            {
                Id = Guid.NewGuid(),
                CapturedAtUtc = DateTime.UtcNow.AddDays(-2),
                TotalUsers = 10,
                ActiveUsers = 9,
                TotpEnabled = 5,
                TotpRequired = 7,
                LockedOut = 1,
                PendingPasswordResets = 0,
                ActiveSessions = 8,
                CreatedBy = "tests"
            },
            new SecurityMetricSnapshot
            {
                Id = Guid.NewGuid(),
                CapturedAtUtc = DateTime.UtcNow.AddDays(-10),
                TotalUsers = 12,
                ActiveUsers = 10,
                TotpEnabled = 6,
                TotpRequired = 8,
                LockedOut = 2,
                PendingPasswordResets = 1,
                ActiveSessions = 9,
                CreatedBy = "tests"
            });
        await dbContext.SaveChangesAsync();

        var metricsProvider = new Mock<ISecurityMetricsProvider>();
        var alertStore = new Mock<ISecurityAlertConfigurationStore>();
        alertStore.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new SecurityAlertConfigurationModel(false, 50, 10, 10, Array.Empty<string>()));
        var emailStore = new Mock<IEmailConnectorStore>();

        var service = new SecurityMetricsSnapshotService(dbContext, metricsProvider.Object, alertStore.Object, emailStore.Object, NullLogger<SecurityMetricsSnapshotService>.Instance);

        var snapshots = await service.GetRecentAsync(5);

        Assert.Single(snapshots);
        Assert.True(snapshots[0].CapturedAtUtc > DateTime.UtcNow.AddDays(-5));
    }
}
