using System;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using HWInventory.Infrastructure.Persistence;
using HWInventory.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace HWInventory.Infrastructure.Tests;

public class SsprServiceTests
{
    [Fact]
    public async Task RequestResetAsync_ReturnsFailure_WhenCaptchaInvalid()
    {
        var (dbContext, userManager) = CreateIdentityContext();
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "alice",
            NormalizedUserName = "ALICE",
            Email = "alice@example.com",
            NormalizedEmail = "ALICE@EXAMPLE.COM",
            DisplayName = "Alice",
            CreatedBy = "tests",
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var service = new SsprService(
            userManager,
            dbContext,
            new StubCaptchaValidator(false),
            new StubSmsGateway(),
            new StubSessionTracker(),
            new StubSsprConfigurationStore(),
            NullLogger<SsprService>.Instance);

        var result = await service.RequestResetAsync(new PasswordResetRequest("alice", "invalid", "Email", null, false), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("CAPTCHA validation failed.", result.Message);
    }

    [Fact]
    public async Task RequestResetAsync_IssuesSmsCode_WhenRequired()
    {
        var (dbContext, userManager) = CreateIdentityContext();
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "bob",
            NormalizedUserName = "BOB",
            Email = "bob@example.com",
            NormalizedEmail = "BOB@EXAMPLE.COM",
            DisplayName = "Bob",
            CreatedBy = "tests",
            CreatedAtUtc = DateTime.UtcNow
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var smsGateway = new StubSmsGateway();
        var configStore = new StubSsprConfigurationStore() { Model = new SsprConfigurationModel(true, false, true, true, 30, 15, 3, "sms") };
        var service = new SsprService(
            userManager,
            dbContext,
            new StubCaptchaValidator(true),
            smsGateway,
            new StubSessionTracker(),
            configStore,
            NullLogger<SsprService>.Instance);

        var result = await service.RequestResetAsync(new PasswordResetRequest("bob", "token", "Email", "+420123456789", true, "127.0.0.1", "tests"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.RequiresSms);
        Assert.NotNull(result.Token);
        Assert.True(smsGateway.LastMessage?.Contains("HWInventory reset kód") ?? false);

        var storedToken = await dbContext.PasswordResetTokens.SingleAsync();
        Assert.Equal(PasswordResetStatus.Pending, storedToken.Status);
        Assert.NotNull(storedToken.SmsCodeHash);
        Assert.Equal("127.0.0.1", storedToken.ClientIp);
        Assert.Equal("tests", storedToken.UserAgent);

        // Verify throttling threshold uses configured window
        configStore.Model = configStore.Model with { MaxRequestsPerWindow = 1 };
        var throttled = await service.RequestResetAsync(new PasswordResetRequest("bob", "token", "Email", "+420123456789", true, "127.0.0.1", "tests"), CancellationToken.None);
        Assert.False(throttled.Success);
        Assert.NotNull(throttled.RetryAfterSeconds);
        Assert.InRange(throttled.RetryAfterSeconds!.Value, 1, 15 * 60);
    }

    private static (AppDbContext dbContext, UserManager<AppUser> userManager) CreateIdentityContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var dbContext = new AppDbContext(options);
        var store = new UserStore<AppUser, AppRole, AppDbContext, Guid>(dbContext);
        var userManager = new UserManager<AppUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<AppUser>(),
            Array.Empty<IUserValidator<AppUser>>(),
            Array.Empty<IPasswordValidator<AppUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null,
            NullLogger<UserManager<AppUser>>.Instance);

        return (dbContext, userManager);
    }

    private sealed class StubCaptchaValidator : ICaptchaValidator
    {
        private readonly bool _result;

        public StubCaptchaValidator(bool result)
        {
            _result = result;
        }

        public Task<bool> ValidateAsync(string token, CancellationToken cancellationToken = default) => Task.FromResult(_result);
    }

    private sealed class StubSmsGateway : ISmsGateway
    {
        public string? LastMessage { get; private set; }

        public Task SendAsync(string destination, string message, CancellationToken cancellationToken)
        {
            LastMessage = message;
            return Task.CompletedTask;
        }
    }

    private sealed class StubSessionTracker : ISessionTracker
    {
        public Task RevokeAllAsync(Guid userId, string actor, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubSsprConfigurationStore : ISsprConfigurationStore
    {
        public SsprConfigurationModel Model { get; set; } = new(true, true, true, false, 30, 15, 3, "sms");

        public Task<SsprConfigurationModel> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Model);

        public Task<SsprConfigurationModel> SaveAsync(SsprConfigurationUpdate update, CancellationToken cancellationToken = default)
        {
            Model = new SsprConfigurationModel(
                update.Enabled,
                update.RequireTwoFactor,
                update.RequireCaptcha,
                update.RequireSmsOtp,
                update.TokenExpiryMinutes,
                update.ThrottleWindowMinutes,
                update.MaxRequestsPerWindow,
                update.SmsConnectorKey);

            return Task.FromResult(Model);
        }
    }
}
