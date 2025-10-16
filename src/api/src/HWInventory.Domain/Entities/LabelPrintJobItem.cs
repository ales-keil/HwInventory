namespace HWInventory.Domain.Entities;

public class LabelPrintJobItem : BaseEntity
{
    public Guid LabelPrintJobId { get; set; }
    public Guid AssetId { get; set; }
    public string AssetType { get; set; } = string.Empty;
    public int Copies { get; set; } = 1;
}
