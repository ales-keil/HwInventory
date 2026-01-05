using System;

namespace HWInventory.Domain.Entities;

public class SecurityMetricSnapshot : AuditableEntity
{
    public DateTime CapturedAtUtc { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotpEnabled { get; set; }
    public int TotpRequired { get; set; }
    public int LockedOut { get; set; }
    public int PendingPasswordResets { get; set; }
    public int ActiveSessions { get; set; }
    public string? AlertsJson { get; set; }
}
