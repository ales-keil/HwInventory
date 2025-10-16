namespace HWInventory.Domain.Entities;

public class UserSession : AuditableEntity
{
    public Guid AppUserId { get; set; }
    public AppUser AppUser { get; set; } = default!;
    public string SessionIdentifier { get; set; } = string.Empty;
    public DateTime IssuedAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedBy { get; set; }
    public string? TerminationReason { get; set; }
}
