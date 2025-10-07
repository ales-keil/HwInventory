namespace HWInventory.Application.Common;

public class FilterDescriptor
{
    public string Field { get; init; } = string.Empty;
    public string Operator { get; init; } = string.Empty;
    public string? Value { get; init; }
}
