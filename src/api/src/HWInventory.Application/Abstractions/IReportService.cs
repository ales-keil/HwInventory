using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Domain.Enums;

namespace HWInventory.Application.Abstractions;

public interface IReportService
{
    Task<ReportDefinitionModel> CreateAsync(CreateReportRequest request, CancellationToken cancellationToken = default);
    Task<ReportDefinitionModel> UpdateAsync(Guid id, UpdateReportRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ReportDefinitionModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportDefinitionModel>> ListAsync(int page, int size, CancellationToken cancellationToken = default);
    Task<ReportRunModel> TriggerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportRunModel>> ListRunsAsync(Guid id, int page, int size, CancellationToken cancellationToken = default);
    Task<ReportRunModel?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default);
    Task<byte[]?> GetArtifactAsync(Guid runId, CancellationToken cancellationToken = default);
    Task ProcessDueReportsAsync(CancellationToken cancellationToken = default);
}

public record CreateReportRequest(
    string Name,
    string? Description,
    ReportScope Scope,
    ExportFormat Format,
    ReportRecurrence Recurrence,
    string? FilterJson,
    string? Recipients,
    string StoragePath,
    TimeSpan? RunAtTime,
    DayOfWeek? RunOnDayOfWeek,
    int? RunOnDayOfMonth,
    bool Enabled);

public record UpdateReportRequest(
    string Name,
    string? Description,
    ReportScope Scope,
    ExportFormat Format,
    ReportRecurrence Recurrence,
    string? FilterJson,
    string? Recipients,
    string StoragePath,
    TimeSpan? RunAtTime,
    DayOfWeek? RunOnDayOfWeek,
    int? RunOnDayOfMonth,
    bool Enabled);

public record ReportDefinitionModel(
    Guid Id,
    string Name,
    string? Description,
    ReportScope Scope,
    ExportFormat Format,
    ReportRecurrence Recurrence,
    string? FilterJson,
    string? Recipients,
    string StoragePath,
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

public record ReportRunModel(
    Guid Id,
    Guid ReportDefinitionId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    ReportRunStatus Status,
    string? ArtifactPath,
    string? FailureReason);
