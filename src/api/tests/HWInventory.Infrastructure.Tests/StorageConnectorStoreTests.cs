using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Infrastructure.Connectors;
using HWInventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HWInventory.Infrastructure.Tests;

public class StorageConnectorStoreTests
{
    [Fact]
    public async Task SaveAsync_CreatesConnectorWithSecrets()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new AppDbContext(options);
        var accessor = new HttpContextAccessor();
        var store = new StorageConnectorStore(dbContext, accessor, NullLogger<StorageConnectorStore>.Instance);

        var update = new StorageConnectorUpdate(
            Alias: "SMB storage",
            Enabled: true,
            Type: "SMB",
            Path: "\\\\fileserver\\share",
            Endpoint: null,
            Bucket: null,
            Folder: "exports",
            Region: null,
            Username: "svc-storage",
            Domain: "AD",
            PublicUrlBase: null,
            RetentionDays: 14,
            UseSsl: null,
            RotateSecret: false,
            Password: "Secret!123",
            AccessKey: null,
            SecretKey: null);

        var result = await store.SaveAsync(update);

        Assert.NotNull(result);
        Assert.Equal("SMB", result.Type);

        var connector = await dbContext.ConnectorProfiles.Include(x => x.Secrets).SingleAsync();
        Assert.Equal("SMB", connector.Type);
        Assert.True(connector.Secrets.Any(secret => secret.Key == "password"));
    }

    [Fact]
    public async Task TestAsync_LocalStorageWritesProbeFile()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new AppDbContext(options);
        var accessor = new HttpContextAccessor();
        var store = new StorageConnectorStore(dbContext, accessor, NullLogger<StorageConnectorStore>.Instance);

        var tempDirectory = Path.Combine(Path.GetTempPath(), "hwinventory-storage-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            await store.SaveAsync(new StorageConnectorUpdate(
                Alias: "Local",
                Enabled: true,
                Type: "LOCAL",
                Path: tempDirectory,
                Endpoint: null,
                Bucket: null,
                Folder: null,
                Region: null,
                Username: null,
                Domain: null,
                PublicUrlBase: null,
                RetentionDays: null,
                UseSsl: null,
                RotateSecret: false,
                Password: null,
                AccessKey: null,
                SecretKey: null));

            var result = await store.TestAsync();

            Assert.True(result.Success);

            var connector = await dbContext.ConnectorProfiles.SingleAsync();
            Assert.Equal("Healthy", connector.HealthStatus);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
