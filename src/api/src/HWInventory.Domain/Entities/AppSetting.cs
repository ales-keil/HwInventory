namespace HWInventory.Domain.Entities;

public class AppSetting : AuditableEntity
{
    public string Section { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public bool IsSecret { get; set; }
    public bool IsMasked => IsSecret && !string.IsNullOrEmpty(Value);
}
