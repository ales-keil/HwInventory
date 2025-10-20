using System;
using System.Collections.Generic;
using HWInventory.Domain.Enums;

namespace HWInventory.Domain.Entities;

public class ReportDefinition : AuditableEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ReportScope Scope { get; set; }
    public ExportFormat Format { get; set; }
    public ReportRecurrence Recurrence { get; set; }
    public TimeSpan? RunAtTime { get; set; }
    public DayOfWeek? RunOnDayOfWeek { get; set; }
    public int? RunOnDayOfMonth { get; set; }
    public string? FilterJson { get; set; }
    public string? Recipients { get; set; }
    public string? StoragePath { get; set; }
    public string EmailSubjectTemplate { get; set; } = "HW Inventory – report {ReportName}";
    public string EmailBodyTemplate { get; set; } = "Report '{ReportName}' ({Scope}) byl {StatusText} v {CompletedAt}." +
        "\n\nSoubor: {ArtifactPath}\nDetaily: {ReportUrl}";
    public bool NotifyOnFailureOnly { get; set; }
    public bool IncludeArtifactInEmail { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTimeOffset? NextRunAtUtc { get; set; }
    public DateTimeOffset? LastRunAtUtc { get; set; }

    public ICollection<ReportRun> Runs { get; set; } = new List<ReportRun>();
}
