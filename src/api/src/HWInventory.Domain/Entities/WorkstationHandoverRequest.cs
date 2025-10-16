using System;
using HWInventory.Domain.Enums;

namespace HWInventory.Domain.Entities;

public class WorkstationHandoverRequest : BaseEntity
{
    public Guid WorkstationId { get; set; }

    public Workstation Workstation { get; set; } = null!;

    public Guid? OldOwnerId { get; set; }

    public string? OldOwnerDisplayName { get; set; }

    public string? OldOwnerDepartment { get; set; }

    public Guid OldLocationId { get; set; }

    public string? OldLocationName { get; set; }

    public string? OldLocationNote { get; set; }

    public Guid? NewOwnerId { get; set; }

    public string? NewOwnerDisplayName { get; set; }

    public string? NewOwnerDepartment { get; set; }

    public Guid NewLocationId { get; set; }

    public string? NewLocationName { get; set; }

    public string? NewLocationNote { get; set; }

    public Guid? NewAdminId { get; set; }

    public string RequestedBy { get; set; } = string.Empty;

    public DateTime RequestedAtUtc { get; set; }

    public WorkstationHandoverStatus Status { get; set; }

    public DateTime TokenExpiresAtUtc { get; set; }

    public string AcceptTokenHash { get; set; } = string.Empty;

    public string DeclineTokenHash { get; set; } = string.Empty;

    public bool Force { get; set; }

    public bool EmailSent { get; set; }

    public string? EmailError { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public string? CompletedBy { get; set; }

    public string ToRecipientsJson { get; set; } = "[]";

    public string CcRecipientsJson { get; set; } = "[]";

    public string BccRecipientsJson { get; set; } = "[]";

    public string? Subject { get; set; }

    public string? MessageBody { get; set; }

    public string? Comment { get; set; }
}
