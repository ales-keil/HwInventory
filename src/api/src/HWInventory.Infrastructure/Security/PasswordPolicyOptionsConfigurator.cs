using System;
using HWInventory.Application.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HWInventory.Infrastructure.Security;

public class PasswordPolicyOptionsConfigurator : IConfigureOptions<IdentityOptions>
{
    private readonly IServiceProvider _serviceProvider;

    public PasswordPolicyOptionsConfigurator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Configure(IdentityOptions options)
    {
        using var scope = _serviceProvider.CreateScope();
        var policyStore = scope.ServiceProvider.GetRequiredService<IPasswordPolicyConfigurationStore>();
        var policy = policyStore.GetAsync().GetAwaiter().GetResult();

        if (!policy.Enabled)
        {
            return;
        }

        options.Password.RequiredLength = Math.Max(8, policy.MinimumLength);
        options.Password.RequireUppercase = policy.RequireUppercase;
        options.Password.RequireLowercase = policy.RequireLowercase;
        options.Password.RequireDigit = policy.RequireDigit;
        options.Password.RequireNonAlphanumeric = policy.RequireNonAlphanumeric;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = Math.Max(1, policy.LockoutAttempts);
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(Math.Max(1, policy.LockoutDurationMinutes));

        if (policy.ExpirationDays > 0)
        {
            options.SecurityStampValidationInterval = TimeSpan.FromDays(policy.ExpirationDays);
        }
    }
}
