using System.Threading;
using System.Threading.Tasks;

namespace HWInventory.Application.Abstractions;

public interface IPasswordPolicyConfigurationStore
{
    Task<PasswordPolicyModel> GetAsync(CancellationToken cancellationToken = default);
    Task<PasswordPolicyModel> SaveAsync(PasswordPolicyUpdate update, CancellationToken cancellationToken = default);
}

public record PasswordPolicyModel(
    bool Enabled,
    int MinimumLength,
    bool RequireUppercase,
    bool RequireLowercase,
    bool RequireDigit,
    bool RequireNonAlphanumeric,
    int ExpirationDays,
    int HistoryCount,
    int LockoutAttempts,
    int LockoutDurationMinutes);

public record PasswordPolicyUpdate(
    bool Enabled,
    int MinimumLength,
    bool RequireUppercase,
    bool RequireLowercase,
    bool RequireDigit,
    bool RequireNonAlphanumeric,
    int ExpirationDays,
    int HistoryCount,
    int LockoutAttempts,
    int LockoutDurationMinutes);
