using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Infrastructure.Persistence;
using HWInventory.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HWInventory.Infrastructure.Tests;

public class SecurityAlertConfigurationStoreTests
{
    [Fact]
    public async Task GetAsync_ReturnsDefaults_WhenNotConfigured()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new AppDbContext(options);
        var accessor = CreateAccessor();
        var store = new SecurityAlertConfigurationStore(dbContext, accessor);

        var configuration = await store.GetAsync();

        Assert.True(configuration.Enabled);
        Assert.Equal(75d, configuration.MinimumTwoFactorAdoptionPercentage);
        Assert.Equal(5, configuration.LockedAccountThreshold);
        Assert.Equal(10, configuration.PendingResetThreshold);
        Assert.Empty(configuration.NotificationEmails);
    }

    [Fact]
    public async Task SaveAsync_PersistsConfiguration()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new AppDbContext(options);
        var accessor = CreateAccessor();
        var store = new SecurityAlertConfigurationStore(dbContext, accessor);

        var update = new SecurityAlertConfigurationUpdate(
            true,
            80,
            3,
            7,
            new List<string> { "soc@example.com", "secops@example.com" });

        var saved = await store.SaveAsync(update);

        Assert.Equal(update.Enabled, saved.Enabled);
        Assert.Equal(update.MinimumTwoFactorAdoptionPercentage, saved.MinimumTwoFactorAdoptionPercentage);
        Assert.Equal(update.LockedAccountThreshold, saved.LockedAccountThreshold);
        Assert.Equal(update.PendingResetThreshold, saved.PendingResetThreshold);
        Assert.Equal(update.NotificationEmails.Count, saved.NotificationEmails.Count);

        var roundtrip = await store.GetAsync();
        Assert.Equal(saved.NotificationEmails, roundtrip.NotificationEmails);
    }

    private static IHttpContextAccessor CreateAccessor()
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "tests") }))
        };

        return new HttpContextAccessor { HttpContext = context };
    }
}
