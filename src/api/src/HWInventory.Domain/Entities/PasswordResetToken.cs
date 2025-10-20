namespace HWInventory.Domain.Entities;

public class PasswordResetToken : AuditableEntity
{
    public Guid AppUserId { get; set; }
    public AppUser AppUser { get; set; } = default!;
    public Guid Token { get; set; }
    public string DeliveryMethod { get; set; } = string.Empty;
    public string? DeliveryAddress { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public string? CaptchaToken { get; set; }
    public string IdentityToken { get; set; } = string.Empty;
    public string? SmsCodeHash { get; set; }
    public bool RequiresSmsVerification { get; set; }
    public string Status { get; set; } = string.Empty;
}
