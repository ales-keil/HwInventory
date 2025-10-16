using System.Collections.Generic;

namespace HWInventory.Domain.Entities;

public class LabelPrintJob : AuditableEntity
{
    public string Target { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string JobStatus { get; set; } = "Pending";
    public string? ArtifactPath { get; set; }
    public ICollection<LabelPrintJobItem> Items { get; set; } = new List<LabelPrintJobItem>();
}
