using System;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Infrastructure.Persistence;
using HWInventory.Infrastructure.Workstations;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HWInventory.Infrastructure.Tests;

public class HandoverConfigurationStoreTests
{
    private readonly AppDbContext _dbContext;
    private readonly IHandoverConfigurationStore _store;

    public HandoverConfigurationStoreTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);
        _store = new HandoverConfigurationStore(_dbContext, new HttpContextAccessor());
    }

    [Fact]
    public async Task GetAsync_ReturnsEmptyDefaults_WhenNotConfigured()
    {
        var configuration = await _store.GetAsync();

        Assert.NotNull(configuration);
        Assert.Empty(configuration.DefaultTo);
        Assert.Empty(configuration.DefaultCc);
        Assert.Empty(configuration.DefaultBcc);
        Assert.False(configuration.UseMinimalPdf);
    }

    [Fact]
    public async Task SaveAsync_NormalizesRecipients_AndPersistsValues()
    {
        var update = new HandoverConfigurationModel(
            new[] { " user@example.com ", "USER@example.com" },
            new[] { "cc1@example.com", "", "cc2@example.com" },
            Array.Empty<string>(),
            "Předání zařízení {Name}",
            "Dobrý den {NewOwner}, prosíme o potvrzení převzetí zařízení {AssetTag}.",
            "VGVzdEJhc2U2NA==",
            true,
            "Připraveno systémem HW Inventory");

        var saved = await _store.SaveAsync(update);

        Assert.Single(saved.DefaultTo);
        Assert.Equal("user@example.com", saved.DefaultTo[0]);
        Assert.Equal(2, saved.DefaultCc.Count);
        Assert.True(saved.UseMinimalPdf);
        Assert.Equal("Připraveno systémem HW Inventory", saved.PdfFooterNote);

        var roundtrip = await _store.GetAsync();
        Assert.Equal(saved.DefaultTo, roundtrip.DefaultTo);
        Assert.Equal(saved.DefaultCc, roundtrip.DefaultCc);
        Assert.Equal(saved.DefaultSubject, roundtrip.DefaultSubject);
    }
}
