using System.Security.Cryptography;
using HWInventory.Application.Abstractions;
using OtpNet;

namespace HWInventory.Infrastructure.Security;

public class TotpService : ITotpService
{
    private const int SecretSize = 20;

    public string GenerateSecretKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(SecretSize);
        return Base32Encoding.ToString(bytes);
    }

    public string GenerateCode(string secretKey)
    {
        var totp = CreateTotp(secretKey);
        return totp.ComputeTotp(DateTime.UtcNow);
    }

    public bool ValidateCode(string secretKey, string code)
    {
        var totp = CreateTotp(secretKey);
        return totp.VerifyTotp(code, out _, VerificationWindow.RfcSpecifiedNetworkDelay);
    }

    private static Totp CreateTotp(string secretKey)
    {
        var key = Base32Encoding.ToBytes(secretKey);
        return new Totp(key, mode: OtpHashMode.Sha1, step: 30, totpSize: 6);
    }
}
