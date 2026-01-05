using System;
using Microsoft.AspNetCore.Http;

namespace HWInventory.Api.Models;

public class UpdatePackageUploadRequest
{
    public IFormFile? Package { get; set; }
    public string? Version { get; set; }
    public bool PreserveDatabaseConfiguration { get; set; } = true;
    public bool CreateBackupBeforeInstall { get; set; } = true;
    public string? BackupStoragePath { get; set; }
    public bool PerformIntegrityCheck { get; set; } = true;
    public bool ConfirmedBackupAvailable { get; set; }
    public string? Notes { get; set; }
}

public record UpdatePackageResponseDto(
    Guid Id,
    string Version,
    string FileName,
    string Status,
    string Sha256,
    bool PreserveDatabaseConfiguration,
    bool CreateBackupBeforeInstall,
    bool PerformIntegrityCheck,
    bool ConfirmedBackupAvailable,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc,
    string CreatedBy,
    string? FailureReason,
    string? ManifestJson,
    string? LogPath);

public record UpdateConfigurationResponseDto(
    string DeploymentRootPath,
    string? WebRootPath,
    bool UseAppOfflineFile,
    bool RunMigrations,
    string? PostDeploymentScript);

public class UpdateConfigurationRequestDto
{
    public string DeploymentRootPath { get; set; } = string.Empty;
    public string? WebRootPath { get; set; }
    public bool UseAppOfflineFile { get; set; }
    public bool RunMigrations { get; set; } = true;
    public string? PostDeploymentScript { get; set; }
}
