using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Domain.Enums;

namespace HWInventory.Application.Abstractions;

public interface IImportService
{
    Task<ImportJobModel> QueueImportAsync(ImportRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImportJobModel>> ListHistoryAsync(int page, int size, CancellationToken cancellationToken = default);
    Task<ImportJobModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task ProcessPendingJobsAsync(CancellationToken cancellationToken = default);
}

public record ImportRequest(
    ImportScope Scope,
    ImportFormat Format,
    ImportConflictStrategy ConflictStrategy,
    bool DryRun,
    string StoragePath,
    string FileName,
    string ContentBase64,
    bool SendEmail,
    string? EmailRecipients,
    string? MappingJson);

public record ImportJobModel(
    Guid Id,
    string Scope,
    string Format,
    string Status,
    string ConflictStrategy,
    bool DryRun,
    string StoragePath,
    string OriginalFileName,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string CreatedBy,
    string? FailureReason,
    long? ProcessedRows,
    long? CreatedRows,
    long? UpdatedRows,
    long? SkippedRows,
    string? ResultLog,
    bool SendEmail,
    string? EmailRecipients);
