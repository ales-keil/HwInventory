namespace HWInventory.Api.Models;

public record DictionaryEntryRequestDto(
    string DictType,
    string Key,
    string Value,
    string? Description,
    int Order);
