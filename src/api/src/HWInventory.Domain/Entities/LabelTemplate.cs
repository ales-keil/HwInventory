namespace HWInventory.Domain.Entities;

public class LabelTemplate : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Format { get; set; } = "ZPL";
    public string Payload { get; set; } = string.Empty;
    public string? Description { get; set; }
}
