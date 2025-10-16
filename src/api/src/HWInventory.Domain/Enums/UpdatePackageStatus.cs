namespace HWInventory.Domain.Enums;

public enum UpdatePackageStatus
{
    Pending = 0,
    Validating = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}
