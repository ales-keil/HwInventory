namespace HWInventory.Domain.Entities;

public class ConnectorSecret : BaseEntity
{
    public Guid ConnectorProfileId { get; set; }
    public ConnectorProfile ConnectorProfile { get; set; } = default!;
    public string Key { get; set; } = string.Empty;
    public string SecretReference { get; set; } = string.Empty;
    public DateTime? RotatedAtUtc { get; set; }
}
