using HWInventory.Domain.Enums;
using HWInventory.Domain.ValueObjects;

namespace HWInventory.Domain.Entities;

public class Server : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string InventoryNumber { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public Guid EnvironmentId { get; set; }
    public Guid WsusPriorityId { get; set; }
    public Guid OperatingSystemId { get; set; }
    public Guid ServerRoleId { get; set; }
    public Guid? PrimaryAdministratorId { get; set; }
    public Guid? SecondaryAdministratorId { get; set; }
    public Guid LocationId { get; set; }
    public string? RackPosition { get; set; }
    public DateTime? PurchasedAt { get; set; }
    public DateTime? SupportUntil { get; set; }
    public string? Notes { get; set; }
    public ICollection<NetworkEndpoint> NetworkAssignments { get; set; } = new List<NetworkEndpoint>();
}
