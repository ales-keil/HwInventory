using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Infrastructure.Connectors;
using HWInventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HWInventory.Infrastructure.Tests;

public class PrintingConnectorStoreTests
{
    private readonly AppDbContext _dbContext;
    private readonly PrintingConnectorStore _store;

    public PrintingConnectorStoreTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("printing-connector-tests")
            .Options;
        _dbContext = new AppDbContext(options);
        var contextAccessor = new HttpContextAccessor();
        _store = new PrintingConnectorStore(_dbContext, contextAccessor, NullLogger<PrintingConnectorStore>.Instance);
    }

    [Fact]
    public async Task SaveAsync_PersistsConfiguration()
    {
        var update = new PrintingConnectorUpdate(
            Alias: "Main Printer",
            Enabled: true,
            Host: "10.0.0.5",
            Port: 9100,
            QueueType: "RAW",
            TimeoutSeconds: 45,
            RetryCount: 2,
            RotateSecret: false,
            SharedSecret: "token123");

        var saved = await _store.SaveAsync(update);

        Assert.NotNull(saved);
        Assert.Equal("10.0.0.5", saved.Host);
        Assert.True(saved.Enabled);
        Assert.Equal(9100, saved.Port);
        Assert.True(saved.HasSecret);

        var model = await _store.GetAsync();
        Assert.NotNull(model);
        Assert.Equal("RAW", model!.QueueType);
        Assert.Equal(45, model.TimeoutSeconds);
    }

    [Fact]
    public async Task TestAsync_ReturnsSuccessWhenConfigured()
    {
        await _store.SaveAsync(new PrintingConnectorUpdate(
            Alias: "Lab Printer",
            Enabled: true,
            Host: "print.local",
            Port: 9100,
            QueueType: "RAW",
            TimeoutSeconds: 30,
            RetryCount: 1,
            RotateSecret: false,
            SharedSecret: null));

        var result = await _store.TestAsync();
        Assert.True(result.Success);
    }
}
