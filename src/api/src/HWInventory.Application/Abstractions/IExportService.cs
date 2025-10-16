using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Domain.Enums;

namespace HWInventory.Application.Abstractions;

public interface IExportService
{
    Task<ExportJobModel> QueueExportAsync(ExportRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExportJobModel>> ListHistoryAsync(int page, int size, CancellationToken cancellationToken = default);
    Task<ExportJobModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task ProcessPendingJobsAsync(CancellationToken cancellationToken = default);
}

public record ExportRequest(
    ExportScope Scope,
    ExportFormat Format,
    string StoragePath,
    string? FilterJson,
    bool SendEmail,
    string? EmailRecipients);

public record ExportJobModel(
    Guid Id,
    string Scope,
    string Format,
    string StoragePath,
    string FileName,
    string Status,
    long? FileSizeBytes,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? ExpiresAtUtc,
    string CreatedBy,
    string? FailureReason,
    bool SendEmail,
    string? EmailRecipients,
    string? ArtifactPath);
