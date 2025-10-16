namespace HWInventory.Api.Models;

public record WorkstationRequestDto(
    string Name,
    string InventoryNumber,
    Guid OperatingSystemId,
    Guid WorkstationTypeId,
    Guid? OwnerId,
    string? OwnerDisplayName,
    string? OwnerDepartment,
    Guid LocationId,
    string? LocationNote,
    string Cpu,
    string Ram,
    string Storage,
    string MacAddress,
    DateTime? PurchasedAt,
    DateTime? SupportUntil,
    Guid? PrimaryAdministratorId,
    Guid? SecondaryAdministratorId,
    string? Notes,
    IReadOnlyCollection<NetworkEndpointDto> NetworkAssignments);
