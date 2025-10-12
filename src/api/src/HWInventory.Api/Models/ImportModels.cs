using System;
using System.ComponentModel.DataAnnotations;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Enums;

namespace HWInventory.Api.Models;

public class QueueImportRequest
{
    [Required]
    public ImportScope Scope { get; set; }

    [Required]
    public ImportFormat Format { get; set; }

    [Required]
    public ImportConflictStrategy ConflictStrategy { get; set; }

    public bool DryRun { get; set; } = true;

    [Required]
    [StringLength(1024)]
    public string StoragePath { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string ContentBase64 { get; set; } = string.Empty;

    public bool SendEmail { get; set; }

    [StringLength(512)]
    public string? EmailRecipients { get; set; }

    [StringLength(2048)]
    public string? MappingJson { get; set; }
}

public record ImportJobResponse(
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
    string? EmailRecipients)
{
    public static ImportJobResponse FromModel(ImportJobModel model) => new(
        model.Id,
        model.Scope,
        model.Format,
        model.Status,
        model.ConflictStrategy,
        model.DryRun,
        model.StoragePath,
        model.OriginalFileName,
        model.CreatedAtUtc,
        model.CompletedAtUtc,
        model.CreatedBy,
        model.FailureReason,
        model.ProcessedRows,
        model.CreatedRows,
        model.UpdatedRows,
        model.SkippedRows,
        model.ResultLog,
        model.SendEmail,
        model.EmailRecipients);
}
