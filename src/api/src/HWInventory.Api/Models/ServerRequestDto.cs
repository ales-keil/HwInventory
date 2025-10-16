namespace HWInventory.Api.Models;

public record ServerRequestDto(
    string Name,
    string InventoryNumber,
    string Manufacturer,
    string Model,
    Guid EnvironmentId,
    Guid WsusPriorityId,
    Guid OperatingSystemId,
    Guid ServerRoleId,
    Guid? PrimaryAdministratorId,
    Guid? SecondaryAdministratorId,
    Guid LocationId,
    string? RackPosition,
    DateTime? PurchasedAt,
    DateTime? SupportUntil,
    string? Notes,
    IReadOnlyCollection<NetworkEndpointDto> NetworkAssignments);
