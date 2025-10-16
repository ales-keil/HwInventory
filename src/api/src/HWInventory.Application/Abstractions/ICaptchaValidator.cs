namespace HWInventory.Application.Abstractions;

public interface ICaptchaValidator
{
    Task<bool> ValidateAsync(string token, CancellationToken cancellationToken = default);
}
