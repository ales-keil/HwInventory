using System;
using System.ComponentModel.DataAnnotations;
using HWInventory.Application.Abstractions;

namespace HWInventory.Api.Models;

public class PasswordResetRequestDto
{
    [Required]
    [MaxLength(256)]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string CaptchaToken { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string DeliveryMethod { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? DeliveryDestination { get; set; }

    public bool RequireSmsOtp { get; set; }

    public PasswordResetRequest ToModel(string? clientIp, string? userAgent)
    {
        return new PasswordResetRequest(
            UsernameOrEmail,
            CaptchaToken ?? string.Empty,
            DeliveryMethod ?? string.Empty,
            DeliveryDestination,
            RequireSmsOtp,
            clientIp,
            userAgent);
    }
}

public class PasswordResetCompletionRequestDto
{
    [Required]
    public Guid Token { get; set; }

    [MaxLength(1024)]
    public string CaptchaToken { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? SmsCode { get; set; }

    [Required]
    [MaxLength(256)]
    public string NewPassword { get; set; } = string.Empty;

    public PasswordResetCompletionRequest ToModel()
    {
        return new PasswordResetCompletionRequest(Token, CaptchaToken ?? string.Empty, SmsCode, NewPassword ?? string.Empty);
    }
}
