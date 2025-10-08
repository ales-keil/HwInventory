namespace HWInventory.Api.Models;

public record NetworkDeviceRequestDto(
    string Name,
    string InventoryNumber,
    Guid DeviceTypeId,
    string Manufacturer,
    string Model,
    Guid LocationId,
    string? RackPosition,
    Guid? PrimaryAdministratorId,
    Guid? SecondaryAdministratorId,
    DateTime? SupportUntil,
    string? Notes,
    IReadOnlyCollection<NetworkEndpointDto> NetworkAssignments);
