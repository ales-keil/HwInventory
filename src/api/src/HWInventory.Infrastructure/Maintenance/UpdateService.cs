using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HWInventory.Infrastructure.Maintenance;

public class UpdateService : IUpdateService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IAppDbContext _dbContext;
    private readonly IBackupService _backupService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UpdateService> _logger;

    public UpdateService(
        IAppDbContext dbContext,
        IBackupService backupService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<UpdateService> logger)
    {
        _dbContext = dbContext;
        _backupService = backupService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<UpdatePackageModel> QueuePackageAsync(UpdatePackageRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.StoragePath))
        {
            throw new ArgumentException("Storage path is required", nameof(request.StoragePath));
        }

        Directory.CreateDirectory(request.StoragePath);

        var package = new UpdatePackage
        {
            Version = string.IsNullOrWhiteSpace(request.Version) ? InferVersion(request.FileName) : request.Version!,
            FileName = string.IsNullOrWhiteSpace(request.FileName)
                ? $"update_{DateTime.UtcNow:yyyyMMddHHmmss}.zip"
                : request.FileName,
            PreserveDatabaseConfiguration = request.PreserveDatabaseConfiguration,
            CreateBackupBeforeInstall = request.CreateBackupBeforeInstall,
            BackupStoragePath = request.BackupStoragePath,
            PerformIntegrityCheck = request.PerformIntegrityCheck,
            ConfirmedBackupAvailable = request.ConfirmedBackupAvailable,
            Notes = request.Notes,
            UpdateStatus = UpdatePackageStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = ResolveActor()
        };

        var storedFileName = $"{package.Id:N}_{package.FileName}";
        var storedPath = Path.Combine(request.StoragePath, storedFileName);
        var stagingPath = Path.Combine(request.StoragePath, $"{package.Id:N}_staging");

        var bytes = Convert.FromBase64String(request.ContentBase64);
        await File.WriteAllBytesAsync(storedPath, bytes, cancellationToken);

        package.StoredPath = storedPath;
        package.StagingPath = stagingPath;
        package.Sha256 = Convert.ToHexString(SHA256.HashData(bytes));

        _dbContext.UpdatePackages.Add(package);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Update balík {Version} zařazen do fronty (soubor {File})", package.Version, package.FileName);

        return Map(package);
    }

    public async Task<IReadOnlyList<UpdatePackageModel>> ListHistoryAsync(int page, int size, CancellationToken cancellationToken = default)
    {
        var packages = await _dbContext.UpdatePackages
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(Math.Max(page - 1, 0) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return packages.Select(Map).ToList();
    }

    public async Task<UpdatePackageModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var package = await _dbContext.UpdatePackages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return package is null ? null : Map(package);
    }

    public async Task<byte[]?> GetLogAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var package = await _dbContext.UpdatePackages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (package?.LogPath is null || !File.Exists(package.LogPath))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(package.LogPath, cancellationToken);
    }

    public async Task ProcessPendingPackagesAsync(CancellationToken cancellationToken = default)
    {
        var pendingPackages = await _dbContext.UpdatePackages
            .Where(x => x.UpdateStatus == UpdatePackageStatus.Pending)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(1)
            .ToListAsync(cancellationToken);

        if (!pendingPackages.Any())
        {
            return;
        }

        foreach (var package in pendingPackages)
        {
            package.UpdateStatus = UpdatePackageStatus.Validating;
            package.ModifiedAtUtc = DateTime.UtcNow;
            package.ModifiedBy = "system";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var package in pendingPackages)
        {
            await ProcessPackageAsync(package, cancellationToken);
        }
    }

    private async Task ProcessPackageAsync(UpdatePackage package, CancellationToken cancellationToken)
    {
        var log = new List<string>();
        var logPath = Path.Combine(Path.GetDirectoryName(package.StoredPath) ?? AppContext.BaseDirectory, $"{package.Id:N}_update.log");

        try
        {
            log.Add(LogLine("Start verifikace aktualizačního balíčku."));
            if (!File.Exists(package.StoredPath))
            {
                throw new FileNotFoundException("Balíček nebyl nalezen na disku.", package.StoredPath);
            }

            if (package.PerformIntegrityCheck)
            {
                var bytes = await File.ReadAllBytesAsync(package.StoredPath, cancellationToken);
                var hash = Convert.ToHexString(SHA256.HashData(bytes));
                log.Add(LogLine($"Výpočet SHA256: {hash}"));
                if (!hash.Equals(package.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Kontrola integrity souboru selhala – hash nesouhlasí.");
                }
            }

            package.UpdateStatus = UpdatePackageStatus.Running;
            package.ModifiedAtUtc = DateTime.UtcNow;
            package.ModifiedBy = "system";
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (package.CreateBackupBeforeInstall)
            {
                var backupPath = package.BackupStoragePath;
                if (string.IsNullOrWhiteSpace(backupPath))
                {
                    backupPath = Path.Combine(Path.GetDirectoryName(package.StoredPath) ?? AppContext.BaseDirectory, "backups");
                }

                Directory.CreateDirectory(backupPath);

                log.Add(LogLine($"Spouštím zálohu před aktualizací do {backupPath}."));
                var backupJob = await _backupService.QueueBackupAsync(new BackupRequest(
                    BackupScope.Full,
                    backupPath,
                    false,
                    null,
                    null,
                    false,
                    null,
                    package.PerformIntegrityCheck,
                    true),
                    cancellationToken);

                await _backupService.ProcessPendingJobsAsync(cancellationToken);
                log.Add(LogLine($"Záloha {backupJob.Id} byla dokončena se stavem {backupJob.Status}."));
            }

            if (Directory.Exists(package.StagingPath))
            {
                Directory.Delete(package.StagingPath, true);
            }

            Directory.CreateDirectory(package.StagingPath);
            log.Add(LogLine($"Extrahuji balíček do {package.StagingPath}."));
            ZipFile.ExtractToDirectory(package.StoredPath, package.StagingPath, true);

            var manifestPath = ResolveManifestPath(package.StagingPath);
            if (manifestPath is not null)
            {
                var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
                package.ManifestJson = manifestJson;
                log.Add(LogLine($"Načten manifest {Path.GetFileName(manifestPath)}."));

                var manifest = JsonSerializer.Deserialize<UpdateManifest>(manifestJson, JsonOptions);
                if (!string.IsNullOrWhiteSpace(manifest?.Version))
                {
                    package.Version = manifest.Version!;
                    log.Add(LogLine($"Verze dle manifestu: {manifest.Version}"));
                }

                if (manifest?.Migrations is { Length: > 0 })
                {
                    log.Add(LogLine($"Manifest deklaruje {manifest.Migrations.Length} migrací."));
                }
            }
            else
            {
                log.Add(LogLine("Manifest nebyl nalezen – pokračuji bez něj."));
            }

            package.UpdateStatus = UpdatePackageStatus.Completed;
            package.CompletedAtUtc = DateTime.UtcNow;
            package.ModifiedAtUtc = DateTime.UtcNow;
            package.ModifiedBy = "system";
            package.FailureReason = null;
            log.Add(LogLine("Aktualizace připravena ve staging adresáři. Nasazení je možné dokončit publikací obsahu dle dokumentace."));
        }
        catch (Exception ex)
        {
            package.UpdateStatus = UpdatePackageStatus.Failed;
            package.CompletedAtUtc = DateTime.UtcNow;
            package.ModifiedAtUtc = DateTime.UtcNow;
            package.ModifiedBy = "system";
            package.FailureReason = ex.Message;
            log.Add(LogLine($"Chyba: {ex.Message}"));
            _logger.LogError(ex, "Zpracování aktualizačního balíčku {PackageId} selhalo", package.Id);
        }
        finally
        {
            package.LogPath = logPath;
            await File.WriteAllLinesAsync(logPath, log, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static string InferVersion(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "vNext";
        }

        var parts = Path.GetFileNameWithoutExtension(fileName).Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        var versionPart = parts.LastOrDefault(part => part.Any(char.IsDigit));
        return string.IsNullOrWhiteSpace(versionPart) ? "vNext" : versionPart;
    }

    private static string? ResolveManifestPath(string stagingPath)
    {
        var manifestCandidates = new[]
        {
            Path.Combine(stagingPath, "update-manifest.json"),
            Path.Combine(stagingPath, "manifest.json"),
            Path.Combine(stagingPath, "docs", "update-manifest.json")
        };

        return manifestCandidates.FirstOrDefault(File.Exists);
    }

    private string ResolveActor()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity is not null && user.Identity.IsAuthenticated)
        {
            return user.Identity.Name ?? "unknown";
        }

        return "system";
    }

    private static string LogLine(string message) => $"[{DateTime.UtcNow:O}] {message}";

    private static UpdatePackageModel Map(UpdatePackage package) => new(
        package.Id,
        package.Version,
        package.FileName,
        package.UpdateStatus.ToString(),
        package.Sha256,
        package.StoredPath,
        package.StagingPath,
        package.PreserveDatabaseConfiguration,
        package.CreateBackupBeforeInstall,
        package.PerformIntegrityCheck,
        package.ConfirmedBackupAvailable,
        package.Notes,
        package.CreatedAtUtc,
        package.CompletedAtUtc,
        package.CreatedBy,
        package.FailureReason,
        package.ManifestJson,
        package.LogPath);

    private sealed record UpdateManifest(string? Version, string? Description, string? RequiredDbVersion, string[]? Migrations);
}
