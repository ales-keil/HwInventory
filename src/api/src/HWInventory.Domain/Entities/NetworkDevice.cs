using HWInventory.Domain.ValueObjects;

namespace HWInventory.Domain.Entities;

public class NetworkDevice : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string InventoryNumber { get; set; } = string.Empty;
    public Guid DeviceTypeId { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public string? RackPosition { get; set; }
    public Guid? PrimaryAdministratorId { get; set; }
    public Guid? SecondaryAdministratorId { get; set; }
    public DateTime? SupportUntil { get; set; }
    public string? Notes { get; set; }
    public ICollection<NetworkEndpoint> NetworkAssignments { get; set; } = new List<NetworkEndpoint>();
}
