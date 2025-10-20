using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HWInventory.Infrastructure.Security;

public class SsprService : ISsprService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IAppDbContext _dbContext;
    private readonly ICaptchaValidator _captchaValidator;
    private readonly ISmsGateway _smsGateway;
    private readonly ISessionTracker _sessionTracker;
    private readonly ISsprConfigurationStore _configurationStore;
    private readonly ILogger<SsprService> _logger;

    public SsprService(
        UserManager<AppUser> userManager,
        IAppDbContext dbContext,
        ICaptchaValidator captchaValidator,
        ISmsGateway smsGateway,
        ISessionTracker sessionTracker,
        ISsprConfigurationStore configurationStore,
        ILogger<SsprService> logger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _captchaValidator = captchaValidator;
        _smsGateway = smsGateway;
        _sessionTracker = sessionTracker;
        _configurationStore = configurationStore;
        _logger = logger;
    }

    public async Task<PasswordResetRequestResult> RequestResetAsync(PasswordResetRequest request, CancellationToken cancellationToken = default)
    {
        var configuration = await _configurationStore.GetAsync(cancellationToken);

        if (!configuration.Enabled)
        {
            return new PasswordResetRequestResult(false, "Self-service reset hesla je vypnutý. Kontaktujte administrátora.", null, false);
        }

        if (configuration.RequireCaptcha)
        {
            if (string.IsNullOrWhiteSpace(request.CaptchaToken) || !await _captchaValidator.ValidateAsync(request.CaptchaToken, cancellationToken))
            {
                return new PasswordResetRequestResult(false, "CAPTCHA validation failed.", null, false);
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.CaptchaToken))
        {
            // Volitelná CAPTCHA byla poskytnuta – ověříme ji, abychom zachovali konzistenci chování.
            if (!await _captchaValidator.ValidateAsync(request.CaptchaToken, cancellationToken))
            {
                return new PasswordResetRequestResult(false, "CAPTCHA validation failed.", null, false);
            }
        }

        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.Email == request.UsernameOrEmail || x.UserName == request.UsernameOrEmail, cancellationToken);
        if (user is null)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken); // mitigate enumeration
            return new PasswordResetRequestResult(false, "User not found.", null, false);
        }

        if (configuration.RequireTwoFactor && !user.TwoFactorEnabled)
        {
            return new PasswordResetRequestResult(false, "Pro self-service reset je nutné mít aktivní dvoufaktorové ověření.", null, false);
        }

        var now = DateTime.UtcNow;
        var throttleWindowMinutes = Math.Max(configuration.ThrottleWindowMinutes, 1);
        if (configuration.MaxRequestsPerWindow > 0)
        {
            var windowStart = now.AddMinutes(-throttleWindowMinutes);
            var userAttemptsQuery = _dbContext.PasswordResetTokens
                .Where(x => x.AppUserId == user.Id && x.CreatedAtUtc >= windowStart);
            var userAttempts = await userAttemptsQuery.CountAsync(cancellationToken);
            if (userAttempts >= configuration.MaxRequestsPerWindow)
            {
                var oldest = await userAttemptsQuery
                    .OrderBy(x => x.CreatedAtUtc)
                    .Select(x => x.CreatedAtUtc)
                    .FirstAsync(cancellationToken);
                var retryAfter = ComputeRetryAfterSeconds(now, oldest, throttleWindowMinutes);
                return new PasswordResetRequestResult(false, "Byl překročen limit žádostí o reset hesla. Zkuste to znovu později.", null, false, retryAfter);
            }

            if (!string.IsNullOrWhiteSpace(request.ClientIp))
            {
                var ipAttemptsQuery = _dbContext.PasswordResetTokens
                    .Where(x => x.ClientIp == request.ClientIp && x.CreatedAtUtc >= windowStart);
                var ipAttempts = await ipAttemptsQuery.CountAsync(cancellationToken);
                if (ipAttempts >= configuration.MaxRequestsPerWindow)
                {
                    var oldest = await ipAttemptsQuery
                        .OrderBy(x => x.CreatedAtUtc)
                        .Select(x => x.CreatedAtUtc)
                        .FirstAsync(cancellationToken);
                    var retryAfter = ComputeRetryAfterSeconds(now, oldest, throttleWindowMinutes);
                    return new PasswordResetRequestResult(false, "Z této adresy bylo odesláno příliš mnoho požadavků. Zkuste to znovu později.", null, false, retryAfter);
                }
            }
        }

        var identityToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetToken = new PasswordResetToken
        {
            AppUserId = user.Id,
            Token = Guid.NewGuid(),
            DeliveryMethod = request.DeliveryMethod,
            DeliveryAddress = request.DeliveryDestination,
            ExpiresAtUtc = now.AddMinutes(configuration.TokenExpiryMinutes <= 0 ? 30 : configuration.TokenExpiryMinutes),
            RequiresSmsVerification = configuration.RequireSmsOtp || request.RequireSmsOtp,
            Status = PasswordResetStatus.Pending,
            CreatedBy = user.UserName ?? user.Email ?? "system",
            ModifiedBy = user.UserName ?? user.Email ?? "system",
            CaptchaToken = request.CaptchaToken,
            IdentityToken = identityToken,
            ClientIp = request.ClientIp,
            UserAgent = NormalizeUserAgent(request.UserAgent)
        };

        string? smsCode = null;
        var smsRequired = resetToken.RequiresSmsVerification;
        if (smsRequired)
        {
            if (string.IsNullOrWhiteSpace(configuration.SmsConnectorKey))
            {
                return new PasswordResetRequestResult(false, "SMS konektor pro SSPR není nakonfigurován.", null, false);
            }

            if (string.IsNullOrWhiteSpace(request.DeliveryDestination))
            {
                return new PasswordResetRequestResult(false, "Pro ověření pomocí SMS je nutné zadat telefonní číslo.", null, false);
            }

            smsCode = GenerateSmsCode();
            resetToken.SmsCodeHash = HashValue(smsCode);
            await _smsGateway.SendAsync(request.DeliveryDestination, $"HWInventory reset kód: {smsCode}", cancellationToken);
        }

        _dbContext.PasswordResetTokens.Add(resetToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PasswordResetRequestResult(true, "Reset token issued.", resetToken.Token, smsRequired);
    }

    public async Task<PasswordResetCompletionResult> CompleteResetAsync(PasswordResetCompletionRequest request, CancellationToken cancellationToken = default)
    {
        var configuration = await _configurationStore.GetAsync(cancellationToken);

        if (configuration.RequireCaptcha)
        {
            if (string.IsNullOrWhiteSpace(request.CaptchaToken) || !await _captchaValidator.ValidateAsync(request.CaptchaToken, cancellationToken))
            {
                return new PasswordResetCompletionResult(false, "CAPTCHA validation failed.");
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.CaptchaToken))
        {
            if (!await _captchaValidator.ValidateAsync(request.CaptchaToken, cancellationToken))
            {
                return new PasswordResetCompletionResult(false, "CAPTCHA validation failed.");
            }
        }

        var token = await _dbContext.PasswordResetTokens
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.Token == request.Token, cancellationToken);

        if (token is null)
        {
            return new PasswordResetCompletionResult(false, "Reset token not found.");
        }

        if (token.Status != PasswordResetStatus.Pending)
        {
            return new PasswordResetCompletionResult(false, "Reset token is not active.");
        }

        if (token.ExpiresAtUtc < DateTime.UtcNow)
        {
            token.Status = PasswordResetStatus.Expired;
            token.ModifiedBy = token.ModifiedBy ?? "system";
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new PasswordResetCompletionResult(false, "Reset token expired.");
        }

        if (token.RequiresSmsVerification)
        {
            if (string.IsNullOrWhiteSpace(request.SmsCode) || token.SmsCodeHash != HashValue(request.SmsCode))
            {
                return new PasswordResetCompletionResult(false, "SMS code invalid.");
            }
        }

        var user = token.AppUser;
        var result = await _userManager.ResetPasswordAsync(user, token.IdentityToken, request.NewPassword);
        if (!result.Succeeded)
        {
            var message = string.Join(",", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Failed to reset password for user {UserId}: {Message}", user.Id, message);
            return new PasswordResetCompletionResult(false, message);
        }

        token.Status = PasswordResetStatus.Completed;
        token.ConsumedAtUtc = DateTime.UtcNow;
        token.ModifiedBy = user.UserName ?? user.Email ?? "system";
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _sessionTracker.RevokeAllAsync(user.Id, user.UserName ?? user.Email ?? "system", cancellationToken);

        return new PasswordResetCompletionResult(true, "Password reset successfully.");
    }

    private static string? NormalizeUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        return userAgent.Length <= 512 ? userAgent : userAgent[..512];
    }

    private static int ComputeRetryAfterSeconds(DateTime now, DateTime oldestAttempt, int windowMinutes)
    {
        var retryAt = oldestAttempt.AddMinutes(windowMinutes);
        var seconds = (int)Math.Ceiling((retryAt - now).TotalSeconds);
        return seconds > 0 ? seconds : windowMinutes * 60;
    }

    private static string GenerateSmsCode()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(4);
        var value = BitConverter.ToUInt32(randomBytes, 0) % 1000000;
        return value.ToString("D6");
    }

    private static string HashValue(string value)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(bytes);
    }
}
