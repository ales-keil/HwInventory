namespace HWInventory.Application.Abstractions;

public interface ISsprService
{
    Task<PasswordResetRequestResult> RequestResetAsync(PasswordResetRequest request, CancellationToken cancellationToken = default);
    Task<PasswordResetCompletionResult> CompleteResetAsync(PasswordResetCompletionRequest request, CancellationToken cancellationToken = default);
}

public record PasswordResetRequest(
    string UsernameOrEmail,
    string CaptchaToken,
    string DeliveryMethod,
    string? DeliveryDestination,
    bool RequireSmsOtp,
    string? ClientIp = null,
    string? UserAgent = null);

public record PasswordResetRequestResult(bool Success, string Message, Guid? Token, bool RequiresSms, int? RetryAfterSeconds = null);

public record PasswordResetCompletionRequest(
    Guid Token,
    string CaptchaToken,
    string? SmsCode,
    string NewPassword);

public record PasswordResetCompletionResult(bool Success, string Message);
