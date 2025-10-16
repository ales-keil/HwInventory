using System;
using HWInventory.Domain.Enums;

namespace HWInventory.Domain.Entities;

public class UpdatePackage : AuditableEntity
{
    public string Version { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string StagingPath { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string? ManifestJson { get; set; }
    public UpdatePackageStatus UpdateStatus { get; set; } = UpdatePackageStatus.Pending;
    public bool PreserveDatabaseConfiguration { get; set; }
    public bool CreateBackupBeforeInstall { get; set; }
    public string? BackupStoragePath { get; set; }
    public bool PerformIntegrityCheck { get; set; }
    public bool ConfirmedBackupAvailable { get; set; }
    public string? Notes { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? FailureReason { get; set; }
    public string? LogPath { get; set; }
}
