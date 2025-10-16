namespace HWInventory.Domain.Entities;

public class ConnectorProfile : AuditableEntity
{
    public string Type { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? ConfigurationJson { get; set; }
    public string? HealthStatus { get; set; }
    public DateTime? LastTestedAtUtc { get; set; }
    public ICollection<ConnectorSecret> Secrets { get; set; } = new List<ConnectorSecret>();
}
