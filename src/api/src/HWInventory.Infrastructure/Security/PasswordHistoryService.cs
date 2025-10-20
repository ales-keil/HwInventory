using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Infrastructure.Security;

public class PasswordHistoryService : IPasswordHistoryService
{
    private readonly IAppDbContext _dbContext;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly IPasswordPolicyConfigurationStore _policyStore;

    public PasswordHistoryService(
        IAppDbContext dbContext,
        IPasswordHasher<AppUser> passwordHasher,
        IPasswordPolicyConfigurationStore policyStore)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _policyStore = policyStore;
    }

    public async Task RecordAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        var policy = await _policyStore.GetAsync(cancellationToken);
        if (!policy.Enabled)
        {
            return;
        }

        var entry = new PasswordHistoryEntry
        {
            UserId = user.Id,
            PasswordHash = user.PasswordHash ?? string.Empty,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.PasswordHistoryEntries.Add(entry);

        var keepCount = Math.Max(policy.HistoryCount, 0);
        if (keepCount > 0)
        {
            var obsolete = await _dbContext.PasswordHistoryEntries
                .Where(x => x.UserId == user.Id)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Skip(keepCount)
                .ToListAsync(cancellationToken);

            if (obsolete.Count > 0)
            {
                _dbContext.PasswordHistoryEntries.RemoveRange(obsolete);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsReusedAsync(AppUser user, string password, CancellationToken cancellationToken = default)
    {
        var policy = await _policyStore.GetAsync(cancellationToken);
        if (!policy.Enabled || policy.HistoryCount <= 0)
        {
            return false;
        }

        var previous = await _dbContext.PasswordHistoryEntries
            .Where(x => x.UserId == user.Id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(policy.HistoryCount)
            .ToListAsync(cancellationToken);

        foreach (var entry in previous)
        {
            var verification = _passwordHasher.VerifyHashedPassword(user, entry.PasswordHash, password);
            if (verification == PasswordVerificationResult.Success || verification == PasswordVerificationResult.SuccessRehashRequired)
            {
                return true;
            }
        }

        return false;
    }
}
