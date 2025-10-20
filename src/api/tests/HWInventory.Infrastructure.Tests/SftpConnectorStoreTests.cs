using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Infrastructure.Connectors;
using HWInventory.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HWInventory.Infrastructure.Tests;

public class SftpConnectorStoreTests
{
    private readonly AppDbContext _dbContext;
    private readonly SftpConnectorStore _store;

    public SftpConnectorStoreTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("sftp-connector-tests")
            .Options;
        _dbContext = new AppDbContext(options);
        var contextAccessor = new HttpContextAccessor();
        _store = new SftpConnectorStore(_dbContext, contextAccessor, NullLogger<SftpConnectorStore>.Instance);
    }

    [Fact]
    public async Task SaveAsync_PersistsConfigurationAndSecrets()
    {
        var update = new SftpConnectorUpdate(
            Alias: "File Transfer",
            Enabled: true,
            Protocol: "SFTP",
            Host: "sftp.example.com",
            Port: 22,
            RemotePath: "/upload",
            Username: "robot",
            UseKeyAuthentication: true,
            PassiveMode: false,
            UseImplicitFtps: null,
            AllowUnknownHosts: false,
            RotateSecrets: false,
            Password: "secret",
            PrivateKey: "--BEGIN KEY--",
            KnownHostsFingerprint: "SHA256:abc");

        var saved = await _store.SaveAsync(update);

        Assert.NotNull(saved);
        Assert.Equal("sftp.example.com", saved.Host);
        Assert.True(saved.HasPassword);
        Assert.True(saved.HasPrivateKey);

        var model = await _store.GetAsync();
        Assert.NotNull(model);
        Assert.Equal("SFTP", model!.Protocol);
        Assert.Equal(22, model.Port);
    }

    [Fact]
    public async Task TestAsync_ReturnsSuccessWhenConfigured()
    {
        await _store.SaveAsync(new SftpConnectorUpdate(
            Alias: "Test",
            Enabled: true,
            Protocol: "SFTP",
            Host: "test.example.com",
            Port: 22,
            RemotePath: "/",
            Username: "robot",
            UseKeyAuthentication: false,
            PassiveMode: false,
            UseImplicitFtps: null,
            AllowUnknownHosts: true,
            RotateSecrets: false,
            Password: "secret",
            PrivateKey: null,
            KnownHostsFingerprint: null));

        var result = await _store.TestAsync();
        Assert.True(result.Success);
    }
}
