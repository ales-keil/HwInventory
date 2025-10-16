using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HWInventory.Infrastructure.Security;

public class AppUserManager : UserManager<AppUser>
{
    private readonly IPasswordHistoryService _passwordHistoryService;

    public AppUserManager(
        IUserStore<AppUser> store,
        IOptions<IdentityOptions> optionsAccessor,
        IPasswordHasher<AppUser> passwordHasher,
        IEnumerable<IUserValidator<AppUser>> userValidators,
        IEnumerable<IPasswordValidator<AppUser>> passwordValidators,
        ILookupNormalizer keyNormalizer,
        IdentityErrorDescriber errors,
        IServiceProvider services,
        ILogger<UserManager<AppUser>> logger,
        IPasswordHistoryService passwordHistoryService)
        : base(store, optionsAccessor, passwordHasher, userValidators, passwordValidators, keyNormalizer, errors, services, logger)
    {
        _passwordHistoryService = passwordHistoryService;
    }

    protected override async Task<IdentityResult> UpdatePasswordHash(AppUser user, string? newPassword, bool validatePassword)
    {
        var result = await base.UpdatePasswordHash(user, newPassword, validatePassword);
        if (result.Succeeded && !string.IsNullOrEmpty(user.PasswordHash))
        {
            await _passwordHistoryService.RecordAsync(user);
        }

        return result;
    }
}
