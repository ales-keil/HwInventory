namespace HWInventory.Domain.ValueObjects;

public class NetworkEndpoint
{
    public string Label { get; set; } = string.Empty;
    public Guid? VlanId { get; set; }
    public string? IpAddress { get; set; }
}
