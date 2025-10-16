using System;
using HWInventory.Domain.Enums;

namespace HWInventory.Domain.Entities;

public class ReportRun : BaseEntity
{
    public Guid Id { get; set; }
    public Guid ReportDefinitionId { get; set; }
    public ReportDefinition ReportDefinition { get; set; } = null!;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public ReportRunStatus Status { get; set; }
    public string? ArtifactPath { get; set; }
    public string? FailureReason { get; set; }
    public string? DownloadToken { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedAtUtc { get; set; }
    public string? ModifiedBy { get; set; }
}
