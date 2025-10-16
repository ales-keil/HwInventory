using System;
using HWInventory.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace HWInventory.Infrastructure.Security;

public class DataProtectionSecretProtector : ISecretProtector
{
    private readonly IDataProtector _protector;

    public DataProtectionSecretProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("HWInventory.SecretStorage");
    }

    public string Protect(string secret)
    {
        if (string.IsNullOrEmpty(secret))
        {
            throw new ArgumentException("Secret must be provided.", nameof(secret));
        }

        return _protector.Protect(secret);
    }

    public string Unprotect(string protectedSecret)
    {
        if (string.IsNullOrEmpty(protectedSecret))
        {
            throw new ArgumentException("Protected secret must be provided.", nameof(protectedSecret));
        }

        return _protector.Unprotect(protectedSecret);
    }
}
