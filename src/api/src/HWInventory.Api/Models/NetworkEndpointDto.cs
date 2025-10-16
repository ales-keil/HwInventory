namespace HWInventory.Api.Models;

public record NetworkEndpointDto(string Label, Guid? VlanId, string? IpAddress);
