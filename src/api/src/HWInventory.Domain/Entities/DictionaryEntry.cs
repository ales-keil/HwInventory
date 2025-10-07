using HWInventory.Domain.Enums;

namespace HWInventory.Domain.Entities;

public class DictionaryEntry : AuditableEntity
{
    public string DictType { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Order { get; set; }
}
