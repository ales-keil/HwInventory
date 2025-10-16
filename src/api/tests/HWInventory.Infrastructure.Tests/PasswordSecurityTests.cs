using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using HWInventory.Infrastructure.Persistence;
using HWInventory.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Xunit;

namespace HWInventory.Infrastructure.Tests;

public class PasswordSecurityTests
{
    [Fact]
    public async Task PasswordHistoryService_RecordsAndTrimsEntries()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new AppDbContext(options);
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "alice",
            NormalizedUserName = "ALICE",
            Email = "alice@example.com",
            NormalizedEmail = "ALICE@EXAMPLE.COM",
            DisplayName = "Alice",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "tests"
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var policyStore = new FakePasswordPolicyStore(new PasswordPolicyModel(true, 12, true, true, true, false, 90, 2, 5, 15));
        var hasher = new PasswordHasher<AppUser>();
        var service = new PasswordHistoryService(dbContext, hasher, policyStore);

        // Act
        user.PasswordHash = hasher.HashPassword(user, "Password#1");
        await service.RecordAsync(user);

        user.PasswordHash = hasher.HashPassword(user, "Password#2");
        await service.RecordAsync(user);

        user.PasswordHash = hasher.HashPassword(user, "Password#3");
        await service.RecordAsync(user);

        // Assert
        var entries = await dbContext.PasswordHistoryEntries
            .Where(x => x.UserId == user.Id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();

        Assert.Equal(2, entries.Count);
        Assert.All(entries, entry => Assert.Equal(user.Id, entry.UserId));
    }

    [Fact]
    public async Task PasswordHistoryValidator_RejectsReusedPasswords()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new AppDbContext(options);
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = "bob",
            NormalizedUserName = "BOB",
            Email = "bob@example.com",
            NormalizedEmail = "BOB@EXAMPLE.COM",
            DisplayName = "Bob",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = "tests"
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var policy = new PasswordPolicyModel(true, 12, true, true, true, false, 90, 5, 5, 15);
        var policyStore = new FakePasswordPolicyStore(policy);
        var hasher = new PasswordHasher<AppUser>();
        var historyService = new PasswordHistoryService(dbContext, hasher, policyStore);
        var validator = new PasswordHistoryValidator(historyService);

        var password = "Secure#Password1";
        user.PasswordHash = hasher.HashPassword(user, password);
        await historyService.RecordAsync(user);

        // Act
        var result = await validator.ValidateAsync(CreateUserManager(), user, password);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "PasswordReused");
    }

    [Fact]
    public void PasswordPolicyOptionsConfigurator_AppliesPolicyToIdentityOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var policy = new PasswordPolicyModel(true, 14, true, true, true, true, 120, 4, 7, 20);
        services.AddScoped<IPasswordPolicyConfigurationStore>(_ => new FakePasswordPolicyStore(policy));
        var provider = services.BuildServiceProvider();

        var configurator = new PasswordPolicyOptionsConfigurator(provider);
        var options = new IdentityOptions();

        // Act
        configurator.Configure(options);

        // Assert
        Assert.Equal(14, options.Password.RequiredLength);
        Assert.True(options.Password.RequireDigit);
        Assert.True(options.Password.RequireUppercase);
        Assert.True(options.Password.RequireLowercase);
        Assert.True(options.Password.RequireNonAlphanumeric);
        Assert.Equal(7, options.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(20), options.Lockout.DefaultLockoutTimeSpan);
    }

    private static UserManager<AppUser> CreateUserManager()
    {
        var store = new MockUserStore();
        var options = Options.Create(new IdentityOptions());
        return new UserManager<AppUser>(store, options, new PasswordHasher<AppUser>(), Enumerable.Empty<IUserValidator<AppUser>>(), Enumerable.Empty<IPasswordValidator<AppUser>>(), new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, new MockLogger<UserManager<AppUser>>());
    }

    private sealed class FakePasswordPolicyStore : IPasswordPolicyConfigurationStore
    {
        private readonly PasswordPolicyModel _model;

        public FakePasswordPolicyStore(PasswordPolicyModel model)
        {
            _model = model;
        }

        public Task<PasswordPolicyModel> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(_model);

        public Task<PasswordPolicyModel> SaveAsync(PasswordPolicyUpdate update, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class MockUserStore : IUserPasswordStore<AppUser>
    {
        public Task<IdentityResult> CreateAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public void Dispose()
        {
        }

        public Task<AppUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<AppUser?>(null);
        public Task<AppUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult<AppUser?>(null);
        public Task<string?> GetNormalizedUserNameAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.NormalizedUserName);
        public Task<string?> GetPasswordHashAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(user.PasswordHash);
        public Task<string?> GetUserIdAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.Id.ToString());
        public Task<string?> GetUserNameAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
        public Task<bool> HasPasswordAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));
        public Task SetNormalizedUserNameAsync(AppUser user, string? normalizedName, CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task SetPasswordHashAsync(AppUser user, string? passwordHash, CancellationToken cancellationToken)
        {
            user.PasswordHash = passwordHash;
            return Task.CompletedTask;
        }

        public Task SetUserNameAsync(AppUser user, string? userName, CancellationToken cancellationToken)
        {
            user.UserName = userName ?? string.Empty;
            return Task.CompletedTask;
        }

        public Task<IdentityResult> UpdateAsync(AppUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
    }

    private sealed class MockLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) => NullDisposable.Instance;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;

        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
        }

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
