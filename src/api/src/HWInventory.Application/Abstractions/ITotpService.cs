namespace HWInventory.Application.Abstractions;

public interface ITotpService
{
    string GenerateSecretKey();
    string GenerateCode(string secretKey);
    bool ValidateCode(string secretKey, string code);
}
