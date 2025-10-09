using HWInventory.Domain.ValueObjects;

namespace HWInventory.Domain.Entities;

public class Workstation : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string InventoryNumber { get; set; } = string.Empty;
    public Guid OperatingSystemId { get; set; }
    public Guid WorkstationTypeId { get; set; }
    public Guid? OwnerId { get; set; }
    public string? OwnerDisplayName { get; set; }
    public string? OwnerDepartment { get; set; }
    public Guid LocationId { get; set; }
    public string? LocationNote { get; set; }
    public string Cpu { get; set; } = string.Empty;
    public string Ram { get; set; } = string.Empty;
    public string Storage { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public DateTime? PurchasedAt { get; set; }
    public DateTime? SupportUntil { get; set; }
    public Guid? PrimaryAdministratorId { get; set; }
    public Guid? SecondaryAdministratorId { get; set; }
    public string? Notes { get; set; }
    public ICollection<NetworkEndpoint> NetworkAssignments { get; set; } = new List<NetworkEndpoint>();
}
