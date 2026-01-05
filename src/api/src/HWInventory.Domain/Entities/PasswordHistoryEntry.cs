using System;

namespace HWInventory.Domain.Entities;

public class PasswordHistoryEntry : BaseEntity
{
    public Guid UserId { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public AppUser? User { get; set; }
        = null!;
}
