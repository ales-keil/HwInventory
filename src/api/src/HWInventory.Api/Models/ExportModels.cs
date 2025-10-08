using System;

namespace HWInventory.Api.Models;

public class ExportRequestDto
{
    public string Scope { get; set; } = "Servers";
    public string Format { get; set; } = "Csv";
    public string StoragePath { get; set; } = string.Empty;
    public string? FilterJson { get; set; }
    public bool SendEmail { get; set; }
    public string? EmailRecipients { get; set; }
}

public class ExportResponseDto
{
    public Guid Id { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long? FileSizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public bool SendEmail { get; set; }
    public string? EmailRecipients { get; set; }
    public string? ArtifactPath { get; set; }
}
