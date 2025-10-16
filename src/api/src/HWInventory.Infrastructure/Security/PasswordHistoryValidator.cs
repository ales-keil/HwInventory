using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace HWInventory.Infrastructure.Security;

public class PasswordHistoryValidator : IPasswordValidator<AppUser>
{
    private readonly IPasswordHistoryService _passwordHistoryService;

    public PasswordHistoryValidator(IPasswordHistoryService passwordHistoryService)
    {
        _passwordHistoryService = passwordHistoryService;
    }

    public async Task<IdentityResult> ValidateAsync(UserManager<AppUser> manager, AppUser user, string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordRequired",
                Description = "Password cannot be empty."
            });
        }

        if (await _passwordHistoryService.IsReusedAsync(user, password))
        {
            return IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordReused",
                Description = "Password was used recently and cannot be reused."
            });
        }

        return IdentityResult.Success;
    }
}
