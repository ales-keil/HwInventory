using System;
using System.ComponentModel.DataAnnotations;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Enums;

namespace HWInventory.Api.Models;

public class CreateReportRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public ReportScope Scope { get; set; }

    [Required]
    public ExportFormat Format { get; set; }

    [Required]
    public ReportRecurrence Recurrence { get; set; }

    public string? FilterJson { get; set; }

    [MaxLength(500)]
    public string? Recipients { get; set; }

    [Required]
    [MaxLength(500)]
    public string StoragePath { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? EmailSubjectTemplate { get; set; }

    [MaxLength(4000)]
    public string? EmailBodyTemplate { get; set; }

    public bool NotifyOnFailureOnly { get; set; }

    public bool IncludeArtifactInEmail { get; set; }

    public TimeSpan? RunAtTime { get; set; }
    public DayOfWeek? RunOnDayOfWeek { get; set; }
    public int? RunOnDayOfMonth { get; set; }
    public bool Enabled { get; set; } = true;
}

public class UpdateReportRequestDto : CreateReportRequestDto
{
}

public record ReportDefinitionResponse(
    Guid Id,
    string Name,
    string? Description,
    ReportScope Scope,
    ExportFormat Format,
    ReportRecurrence Recurrence,
    string? FilterJson,
    string? Recipients,
    string StoragePath,
    string EmailSubjectTemplate,
    string EmailBodyTemplate,
    bool NotifyOnFailureOnly,
    bool IncludeArtifactInEmail,
    TimeSpan? RunAtTime,
    DayOfWeek? RunOnDayOfWeek,
    int? RunOnDayOfMonth,
    bool Enabled,
    DateTimeOffset? NextRunAtUtc,
    DateTimeOffset? LastRunAtUtc,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    string CreatedBy,
    string? ModifiedBy);

public record ReportRunResponse(
    Guid Id,
    Guid ReportDefinitionId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    ReportRunStatus Status,
    string? ArtifactPath,
    string? FailureReason);

public static class ReportModelMapper
{
    public static CreateReportRequest ToCreateModel(this CreateReportRequestDto dto)
    {
        return new CreateReportRequest(
            dto.Name,
            dto.Description,
            dto.Scope,
            dto.Format,
            dto.Recurrence,
            dto.FilterJson,
            dto.Recipients,
            dto.StoragePath,
            dto.EmailSubjectTemplate,
            dto.EmailBodyTemplate,
            dto.NotifyOnFailureOnly,
            dto.IncludeArtifactInEmail,
            dto.RunAtTime,
            dto.RunOnDayOfWeek,
            dto.RunOnDayOfMonth,
            dto.Enabled);
    }

    public static UpdateReportRequest ToUpdateModel(this UpdateReportRequestDto dto)
    {
        return new UpdateReportRequest(
            dto.Name,
            dto.Description,
            dto.Scope,
            dto.Format,
            dto.Recurrence,
            dto.FilterJson,
            dto.Recipients,
            dto.StoragePath,
            dto.EmailSubjectTemplate,
            dto.EmailBodyTemplate,
            dto.NotifyOnFailureOnly,
            dto.IncludeArtifactInEmail,
            dto.RunAtTime,
            dto.RunOnDayOfWeek,
            dto.RunOnDayOfMonth,
            dto.Enabled);
    }

    public static ReportDefinitionResponse ToResponse(this ReportDefinitionModel model)
    {
        return new ReportDefinitionResponse(
            model.Id,
            model.Name,
            model.Description,
            model.Scope,
            model.Format,
            model.Recurrence,
            model.FilterJson,
            model.Recipients,
            model.StoragePath,
            model.EmailSubjectTemplate,
            model.EmailBodyTemplate,
            model.NotifyOnFailureOnly,
            model.IncludeArtifactInEmail,
            model.RunAtTime,
            model.RunOnDayOfWeek,
            model.RunOnDayOfMonth,
            model.Enabled,
            model.NextRunAtUtc,
            model.LastRunAtUtc,
            model.CreatedAtUtc,
            model.ModifiedAtUtc,
            model.CreatedBy,
            model.ModifiedBy);
    }

    public static ReportRunResponse ToResponse(this ReportRunModel model)
    {
        return new ReportRunResponse(
            model.Id,
            model.ReportDefinitionId,
            model.StartedAtUtc,
            model.CompletedAtUtc,
            model.Status,
            model.ArtifactPath,
            model.FailureReason);
    }
}
