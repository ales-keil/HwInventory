using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
    private readonly IUpdateConfigurationStore _configurationStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<UpdateService> _logger;

    public UpdateService(
        IAppDbContext dbContext,
        IBackupService backupService,
        IUpdateConfigurationStore configurationStore,
        IHttpContextAccessor httpContextAccessor,
        ILogger<UpdateService> logger)
    {
        _dbContext = dbContext;
        _backupService = backupService;
        _configurationStore = configurationStore;
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
            UpdateManifest? manifest = null;
            if (manifestPath is not null)
            {
                var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
                package.ManifestJson = manifestJson;
                log.Add(LogLine($"Načten manifest {Path.GetFileName(manifestPath)}."));

                manifest = JsonSerializer.Deserialize<UpdateManifest>(manifestJson, JsonOptions);
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

            var deploymentConfiguration = await _configurationStore.GetAsync(cancellationToken);
            log.Add(LogLine($"Cílová složka nasazení: {deploymentConfiguration.DeploymentRootPath}"));

            if (manifest?.Migrations is { Length: > 0 })
            {
                if (deploymentConfiguration.RunMigrations)
                {
                    await ApplyManifestMigrationsAsync(manifest, package.StagingPath, log, cancellationToken);
                }
                else
                {
                    log.Add(LogLine("Migrace z manifestu byly přeskočeny (RunMigrations = false)."));
                }
            }

            await DeployPackageAsync(package, deploymentConfiguration, log, cancellationToken);

            package.UpdateStatus = UpdatePackageStatus.Completed;
            package.CompletedAtUtc = DateTime.UtcNow;
            package.ModifiedAtUtc = DateTime.UtcNow;
            package.ModifiedBy = "system";
            package.FailureReason = null;
            log.Add(LogLine("Aktualizační balíček byl úspěšně nasazen do cílové složky."));
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

    private async Task DeployPackageAsync(UpdatePackage package, UpdateDeploymentConfigurationModel configuration, List<string> log, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration.DeploymentRootPath))
        {
            throw new InvalidOperationException("Cílová složka nasazení není nastavena.");
        }

        var publishApiPath = Path.Combine(package.StagingPath, "publish", "api");
        if (!Directory.Exists(publishApiPath))
        {
            throw new InvalidOperationException($"Ve stagingu chybí složka {publishApiPath} – balíček není kompletní.");
        }

        if (package.PreserveDatabaseConfiguration)
        {
            CleanupAppSettings(publishApiPath, log);
        }

        Directory.CreateDirectory(configuration.DeploymentRootPath);

        var offlinePath = configuration.UseAppOfflineFile
            ? CreateAppOfflineFile(configuration.DeploymentRootPath, log)
            : null;

        try
        {
            var copiedApi = await Task.Run(() => CopyDirectory(publishApiPath, configuration.DeploymentRootPath, package.PreserveDatabaseConfiguration, log), cancellationToken);
            log.Add(LogLine($"Do aplikace bylo zkopírováno {copiedApi} souborů."));

            var publishWebPath = Path.Combine(package.StagingPath, "publish", "web");
            if (!string.IsNullOrWhiteSpace(configuration.WebRootPath) && Directory.Exists(publishWebPath))
            {
                Directory.CreateDirectory(configuration.WebRootPath);
                var copiedWeb = await Task.Run(() => CopyDirectory(publishWebPath, configuration.WebRootPath!, false, log), cancellationToken);
                log.Add(LogLine($"Do webové části bylo zkopírováno {copiedWeb} souborů."));
            }
            else if (!string.IsNullOrWhiteSpace(configuration.WebRootPath))
            {
                log.Add(LogLine("Balíček neobsahuje složku publish/web – front-end nebyl upraven."));
            }
        }
        finally
        {
            if (offlinePath is not null)
            {
                try
                {
                    if (File.Exists(offlinePath))
                    {
                        File.Delete(offlinePath);
                    }
                    log.Add(LogLine("Soubor app_offline.htm byl odstraněn."));
                }
                catch (Exception ex)
                {
                    log.Add(LogLine($"Varování: nepodařilo se odstranit app_offline.htm ({ex.Message})."));
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(configuration.PostDeploymentScript))
        {
            if (File.Exists(configuration.PostDeploymentScript))
            {
                await RunPostDeploymentScriptAsync(configuration.PostDeploymentScript!, configuration.DeploymentRootPath, package.StagingPath, log, cancellationToken);
            }
            else
            {
                log.Add(LogLine($"Post-deployment skript {configuration.PostDeploymentScript} nebyl nalezen – krok přeskočen."));
            }
        }
    }

    private async Task ApplyManifestMigrationsAsync(UpdateManifest manifest, string stagingPath, List<string> log, CancellationToken cancellationToken)
    {
        if (manifest.Migrations is null || manifest.Migrations.Length == 0)
        {
            return;
        }

        if (_dbContext is not AppDbContext concreteContext)
        {
            log.Add(LogLine("Migrace deklarované v manifestu nelze spustit – kontext nepodporuje přístup k databázi."));
            return;
        }

        foreach (var migration in manifest.Migrations)
        {
            var relativePath = migration.Replace('/', Path.DirectorySeparatorChar);
            var scriptPath = Path.Combine(stagingPath, relativePath);
            if (!File.Exists(scriptPath))
            {
                log.Add(LogLine($"Migrace {migration} nebyla nalezena ve stagingu."));
                continue;
            }

            log.Add(LogLine($"Spouštím SQL skript {migration}."));
            var scriptContent = await File.ReadAllTextAsync(scriptPath, cancellationToken);
            foreach (var batch in SplitSqlBatches(scriptContent))
            {
                if (string.IsNullOrWhiteSpace(batch))
                {
                    continue;
                }

                await concreteContext.Database.ExecuteSqlRawAsync(batch, cancellationToken);
            }

            log.Add(LogLine($"Skript {migration} byl úspěšně dokončen."));
        }
    }

    private static int CopyDirectory(string sourceDir, string targetDir, bool skipAppSettings, List<string> log)
    {
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            if (skipAppSettings && IsAppSettingsFile(relative))
            {
                log.Add(LogLine($"Přeskakuji {relative} (zachování konfigurace)."));
                continue;
            }

            var destination = Path.Combine(targetDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, true);
            count++;
        }

        return count;
    }

    private static void CleanupAppSettings(string publishApiPath, List<string> log)
    {
        foreach (var file in Directory.EnumerateFiles(publishApiPath, "appsettings*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                File.Delete(file);
                log.Add(LogLine($"Ze stagingu odstraněn {Path.GetFileName(file)} (zachování DB konfigurace)."));
            }
            catch (Exception ex)
            {
                log.Add(LogLine($"Varování: soubor {Path.GetFileName(file)} nebylo možné odstranit ({ex.Message})."));
            }
        }
    }

    private static bool IsAppSettingsFile(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        return fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static string? CreateAppOfflineFile(string deploymentRootPath, List<string> log)
    {
        try
        {
            Directory.CreateDirectory(deploymentRootPath);
            var offlinePath = Path.Combine(deploymentRootPath, "app_offline.htm");
            var content = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Údržba</title></head><body><h1>Probíhá aktualizace aplikace HW Inventory</h1><p>Zkuste to prosím později.</p></body></html>";
            File.WriteAllText(offlinePath, content);
            log.Add(LogLine($"Vytvořen app_offline.htm v {deploymentRootPath}."));
            return offlinePath;
        }
        catch (Exception ex)
        {
            log.Add(LogLine($"Varování: nepodařilo se vytvořit app_offline.htm ({ex.Message})."));
            return null;
        }
    }

    private static async Task RunPostDeploymentScriptAsync(string scriptPath, string deploymentRootPath, string stagingPath, List<string> log, CancellationToken cancellationToken)
    {
        log.Add(LogLine($"Spouštím post-deployment skript {scriptPath}."));

        ProcessStartInfo startInfo;
        if (scriptPath.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            var shell = OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh";
            startInfo = new ProcessStartInfo(shell)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add("-DeploymentRoot");
            startInfo.ArgumentList.Add(deploymentRootPath);
            startInfo.ArgumentList.Add("-StagingPath");
            startInfo.ArgumentList.Add(stagingPath);
        }
        else
        {
            startInfo = new ProcessStartInfo(scriptPath)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(deploymentRootPath);
            startInfo.ArgumentList.Add(stagingPath);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            log.Add(LogLine("Skript se nepodařilo spustit."));
            return;
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (!string.IsNullOrWhiteSpace(output))
        {
            log.Add(LogLine($"SCRIPT OUT: {output.Trim()}"));
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            log.Add(LogLine($"SCRIPT ERR: {error.Trim()}"));
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Post-deployment skript skončil s návratovým kódem {process.ExitCode}.");
        }
    }

    private static IEnumerable<string> SplitSqlBatches(string script)
    {
        var batches = new List<string>();
        using var reader = new StringReader(script);
        var builder = new StringBuilder();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                if (builder.Length > 0)
                {
                    batches.Add(builder.ToString());
                    builder.Clear();
                }
            }
            else
            {
                builder.AppendLine(line);
            }
        }

        if (builder.Length > 0)
        {
            batches.Add(builder.ToString());
        }

        return batches;
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
