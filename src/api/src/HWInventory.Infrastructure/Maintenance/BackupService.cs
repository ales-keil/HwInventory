using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Mail;
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

public class BackupService : IBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IAppDbContext _dbContext;
    private readonly IBackupConfigurationStore _scheduleStore;
    private readonly ISecretProtector _secretProtector;
    private readonly IEmailConnectorStore _emailConnectorStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        IAppDbContext dbContext,
        IBackupConfigurationStore scheduleStore,
        ISecretProtector secretProtector,
        IEmailConnectorStore emailConnectorStore,
        IHttpContextAccessor httpContextAccessor,
        ILogger<BackupService> logger)
    {
        _dbContext = dbContext;
        _scheduleStore = scheduleStore;
        _secretProtector = secretProtector;
        _emailConnectorStore = emailConnectorStore;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<BackupJobModel> QueueBackupAsync(BackupRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.StoragePath))
        {
            throw new ArgumentException("Storage path is required.", nameof(request));
        }

        var storagePath = Path.GetFullPath(request.StoragePath);
        Directory.CreateDirectory(storagePath);

        var fileName = BuildFileName(request.Scope, request.EncryptionEnabled);
        var actor = ResolveActor(request.IsAutomatic);

        var job = new BackupJob
        {
            Scope = request.Scope,
            StoragePath = storagePath,
            FileName = fileName,
            EncryptionEnabled = request.EncryptionEnabled,
            SendEmail = request.SendEmail,
            EmailRecipients = NormalizeRecipients(request.EmailRecipients),
            IntegrityCheckEnabled = request.IntegrityCheckEnabled,
            IsAutomatic = request.IsAutomatic,
            CreatedBy = actor,
            ModifiedBy = actor
        };

        if (request.EncryptionEnabled)
        {
            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                job.ProtectedEncryptionSecret = _secretProtector.Protect(request.Password);
            }
            else if (!string.IsNullOrWhiteSpace(request.ProtectedSecret))
            {
                job.ProtectedEncryptionSecret = request.ProtectedSecret;
            }
            else
            {
                throw new InvalidOperationException("Nelze vytvořit zálohu: chybí heslo pro šifrování.");
            }
        }

        _dbContext.BackupJobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(job);
    }

    public async Task<IReadOnlyList<BackupJobModel>> ListHistoryAsync(int page, int size, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.BackupJobs
            .OrderByDescending(x => x.CreatedAtUtc);

        var jobs = await query
            .Skip(Math.Max(page - 1, 0) * size)
            .Take(size)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return jobs.Select(Map).ToList();
    }

    public async Task<BackupJobModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.BackupJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return job is null ? null : Map(job);
    }

    public async Task<RestoreResult> RestoreAsync(Guid jobId, RestoreRequest request, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.BackupJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null)
        {
            return new RestoreResult(false, "Záloha nebyla nalezena.");
        }

        var filePath = Path.Combine(job.StoragePath, job.FileName);
        if (!File.Exists(filePath))
        {
            return new RestoreResult(false, "Soubor zálohy neexistuje.");
        }

        string? password = null;
        if (job.EncryptionEnabled)
        {
            password = request.Password;
            if (string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(job.ProtectedEncryptionSecret))
            {
                password = _secretProtector.Unprotect(job.ProtectedEncryptionSecret);
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                return new RestoreResult(false, "Pro obnovení šifrované zálohy je nutné zadat heslo.");
            }
        }

        var archiveBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        if (job.EncryptionEnabled)
        {
            archiveBytes = Decrypt(archiveBytes, password!);
        }

        await RestoreFromArchiveAsync(archiveBytes, job.Scope, cancellationToken);

        if (request.PerformIntegrityTest)
        {
            var integrity = await PerformIntegrityTestAsync(archiveBytes, job.Scope, cancellationToken);
            if (!integrity)
            {
                return new RestoreResult(true, "Obnova dokončena, ale integrita archivu nebyla potvrzena.");
            }
        }

        return new RestoreResult(true, "Obnova byla úspěšně dokončena.");
    }

    public async Task<IntegrityTestResult> TestIntegrityAsync(Guid jobId, string? password, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.BackupJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null)
        {
            return new IntegrityTestResult(false, "Záloha nebyla nalezena.", null);
        }

        var filePath = Path.Combine(job.StoragePath, job.FileName);
        if (!File.Exists(filePath))
        {
            return new IntegrityTestResult(false, "Soubor zálohy neexistuje.", null);
        }

        if (job.EncryptionEnabled)
        {
            password ??= job.ProtectedEncryptionSecret is null ? null : _secretProtector.Unprotect(job.ProtectedEncryptionSecret);
            if (string.IsNullOrWhiteSpace(password))
            {
                return new IntegrityTestResult(false, "Pro ověření šifrované zálohy je nutné heslo.", null);
            }
        }

        var archiveBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        if (job.EncryptionEnabled)
        {
            archiveBytes = Decrypt(archiveBytes, password!);
        }

        var passed = await PerformIntegrityTestAsync(archiveBytes, job.Scope, cancellationToken);
        job.IntegrityPassed = passed;
        job.IntegrityCheckedAtUtc = DateTime.UtcNow;
        job.ModifiedBy = ResolveActor(true);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new IntegrityTestResult(true, passed ? "Archiv je v pořádku." : "Archiv se nepodařilo ověřit.", passed);
    }

    public async Task QueueScheduledBackupIfDueAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var trigger = await _scheduleStore.TryMarkTriggeredAsync(DateTime.UtcNow, cancellationToken);
            if (!trigger.Triggered || trigger.Schedule is null)
            {
                return;
            }

            var password = trigger.Schedule.EncryptionEnabled && !string.IsNullOrWhiteSpace(trigger.ProtectedSecret)
                ? _secretProtector.Unprotect(trigger.ProtectedSecret)
                : null;

            var request = new BackupRequest(
                ParseScope(trigger.Schedule.Scope),
                trigger.Schedule.StoragePath,
                trigger.Schedule.EncryptionEnabled,
                password,
                trigger.ProtectedSecret,
                trigger.Schedule.SendEmail,
                trigger.Schedule.EmailRecipients,
                trigger.Schedule.IntegrityCheckEnabled,
                true);

            await QueueBackupAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Naplánovanou zálohu se nepodařilo zařadit do fronty.");
        }
    }

    public async Task ProcessPendingJobsAsync(CancellationToken cancellationToken = default)
    {
        var pendingJobs = await _dbContext.BackupJobs
            .Where(x => x.JobStatus == BackupJobStatus.Pending)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(3)
            .ToListAsync(cancellationToken);

        if (pendingJobs.Count == 0)
        {
            return;
        }

        var actor = ResolveActor(true);
        foreach (var job in pendingJobs)
        {
            job.JobStatus = BackupJobStatus.Running;
            job.ModifiedBy = actor;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var job in pendingJobs)
        {
            await ProcessJobAsync(job, cancellationToken);
        }
    }

    private async Task ProcessJobAsync(BackupJob job, CancellationToken cancellationToken)
    {
        try
        {
            var archiveBytes = await BuildArchiveAsync(job.Scope, cancellationToken);
            var filePath = Path.Combine(job.StoragePath, job.FileName);

            var payloadBytes = archiveBytes;
            string? password = null;
            if (job.EncryptionEnabled)
            {
                password = job.ProtectedEncryptionSecret is null
                    ? throw new InvalidOperationException("Šifrovaná záloha nemá uložené heslo.")
                    : _secretProtector.Unprotect(job.ProtectedEncryptionSecret);

                payloadBytes = Encrypt(archiveBytes, password);
            }

            await File.WriteAllBytesAsync(filePath, payloadBytes, cancellationToken);
            var info = new FileInfo(filePath);
            job.FileSizeBytes = info.Length;
            job.ArtifactPath = filePath;
            job.CompletedAtUtc = DateTime.UtcNow;

            if (job.IntegrityCheckEnabled)
            {
                var integrityBytes = job.EncryptionEnabled && password is not null
                    ? archiveBytes
                    : payloadBytes;

                job.IntegrityPassed = await PerformIntegrityTestAsync(integrityBytes, job.Scope, cancellationToken);
                job.IntegrityCheckedAtUtc = DateTime.UtcNow;
            }

            job.JobStatus = BackupJobStatus.Completed;
            job.FailureReason = null;

            if (job.SendEmail)
            {
                await SendNotificationAsync(job, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zpracování zálohy selhalo");
            job.JobStatus = BackupJobStatus.Failed;
            job.FailureReason = ex.Message;
            job.CompletedAtUtc = DateTime.UtcNow;
        }
        finally
        {
            job.ModifiedBy = ResolveActor(true);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<byte[]> BuildArchiveAsync(BackupScope scope, CancellationToken cancellationToken)
    {
        await using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            await WriteJsonEntryAsync(archive, "metadata.json", new
            {
                Scope = scope.ToString(),
                GeneratedAtUtc = DateTime.UtcNow
            }, cancellationToken);

            if (scope == BackupScope.Full || scope == BackupScope.Dictionaries)
            {
                var dictionaries = await _dbContext.DictionaryEntries.AsNoTracking().ToListAsync(cancellationToken);
                await WriteJsonEntryAsync(archive, "dictionaries.json", dictionaries, cancellationToken);
            }

            if (scope == BackupScope.Full || scope == BackupScope.Servers)
            {
                var servers = await _dbContext.Servers
                    .AsNoTracking()
                    .Include(x => x.NetworkAssignments)
                    .ToListAsync(cancellationToken);
                await WriteJsonEntryAsync(archive, "servers.json", servers, cancellationToken);
            }

            if (scope == BackupScope.Full || scope == BackupScope.NetworkDevices)
            {
                var network = await _dbContext.NetworkDevices
                    .AsNoTracking()
                    .Include(x => x.NetworkAssignments)
                    .ToListAsync(cancellationToken);
                await WriteJsonEntryAsync(archive, "network-devices.json", network, cancellationToken);
            }

            if (scope == BackupScope.Full || scope == BackupScope.Workstations)
            {
                var workstations = await _dbContext.Workstations
                    .AsNoTracking()
                    .Include(x => x.NetworkAssignments)
                    .ToListAsync(cancellationToken);
                await WriteJsonEntryAsync(archive, "workstations.json", workstations, cancellationToken);
            }
        }

        return memory.ToArray();
    }

    private static async Task WriteJsonEntryAsync(ZipArchive archive, string entryName, object data, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await JsonSerializer.SerializeAsync(entryStream, data, JsonOptions, cancellationToken);
    }

    private async Task RestoreFromArchiveAsync(byte[] archiveBytes, BackupScope scope, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(archiveBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, true);

        var dbContext = _dbContext as DbContext;
        if (dbContext is null)
        {
            throw new InvalidOperationException("DbContext neumožňuje transakce.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (scope == BackupScope.Full || scope == BackupScope.Dictionaries)
            {
                await RestoreCollectionAsync<DictionaryEntry>(archive, "dictionaries.json", _dbContext.DictionaryEntries, cancellationToken);
            }

            if (scope == BackupScope.Full || scope == BackupScope.Servers)
            {
                await RestoreCollectionAsync<Server>(archive, "servers.json", _dbContext.Servers, cancellationToken);
            }

            if (scope == BackupScope.Full || scope == BackupScope.NetworkDevices)
            {
                await RestoreCollectionAsync<NetworkDevice>(archive, "network-devices.json", _dbContext.NetworkDevices, cancellationToken);
            }

            if (scope == BackupScope.Full || scope == BackupScope.Workstations)
            {
                await RestoreCollectionAsync<Workstation>(archive, "workstations.json", _dbContext.Workstations, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task RestoreCollectionAsync<TEntity>(ZipArchive archive, string entryName, DbSet<TEntity> set, CancellationToken cancellationToken)
        where TEntity : class
    {
        var entry = archive.GetEntry(entryName);
        if (entry is null)
        {
            return;
        }

        await using var entryStream = entry.Open();
        var items = await JsonSerializer.DeserializeAsync<List<TEntity>>(entryStream, JsonOptions, cancellationToken);
        if (items is null)
        {
            return;
        }

        set.RemoveRange(set);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await set.AddRangeAsync(items, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> PerformIntegrityTestAsync(byte[] archiveBytes, BackupScope scope, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new MemoryStream(archiveBytes);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, true);
            var expectedEntries = new List<string> { "metadata.json" };
            if (scope == BackupScope.Full || scope == BackupScope.Dictionaries)
            {
                expectedEntries.Add("dictionaries.json");
            }

            if (scope == BackupScope.Full || scope == BackupScope.Servers)
            {
                expectedEntries.Add("servers.json");
            }

            if (scope == BackupScope.Full || scope == BackupScope.NetworkDevices)
            {
                expectedEntries.Add("network-devices.json");
            }

            if (scope == BackupScope.Full || scope == BackupScope.Workstations)
            {
                expectedEntries.Add("workstations.json");
            }

            foreach (var entry in expectedEntries)
            {
                if (archive.GetEntry(entry) is null)
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Integrity check failed");
            return false;
        }
    }

    private async Task SendNotificationAsync(BackupJob job, CancellationToken cancellationToken)
    {
        var connector = await _emailConnectorStore.GetAsync(cancellationToken);
        if (connector is null || !connector.Enabled)
        {
            _logger.LogWarning("SMTP konektor není nastaven, notifikace nebude odeslána.");
            return;
        }

        if (string.IsNullOrWhiteSpace(connector.Host))
        {
            _logger.LogWarning("SMTP host není nastaven.");
            return;
        }

        if (string.IsNullOrWhiteSpace(job.EmailRecipients))
        {
            return;
        }

        var recipients = job.EmailRecipients.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (recipients.Length == 0)
        {
            return;
        }

        var password = connector.HasPassword
            ? (await _dbContext.ConnectorProfiles
                .Include(x => x.Secrets)
                .Where(x => x.Type == "SMTP")
                .SelectMany(x => x.Secrets)
                .Select(x => x.SecretReference)
                .FirstOrDefaultAsync(cancellationToken))
            : null;

        try
        {
#pragma warning disable SYSLIB0014
            using var smtp = new SmtpClient(connector.Host, connector.Port)
            {
                EnableSsl = connector.UseTls
            };

            if (!string.IsNullOrWhiteSpace(connector.Username) && !string.IsNullOrWhiteSpace(password))
            {
                smtp.Credentials = new System.Net.NetworkCredential(connector.Username, password);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(connector.FromAddress ?? "no-reply@example.com"),
                Subject = "HW Inventory – záloha dokončena",
                Body = $"Záloha {job.FileName} byla dokončena v {job.CompletedAtUtc:yyyy-MM-dd HH:mm:ss} UTC.",
                IsBodyHtml = false
            };

            foreach (var recipient in recipients)
            {
                message.To.Add(new MailAddress(recipient));
            }

            if (!string.IsNullOrWhiteSpace(job.ArtifactPath) && File.Exists(job.ArtifactPath))
            {
                message.Attachments.Add(new Attachment(job.ArtifactPath));
            }

            await smtp.SendMailAsync(message);
#pragma warning restore SYSLIB0014
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Odeslání notifikace o záloze selhalo");
            job.FailureReason = string.IsNullOrWhiteSpace(job.FailureReason)
                ? $"Notifikace nebyla doručena: {ex.Message}"
                : job.FailureReason + $"; Notifikace: {ex.Message}";
        }
    }

    private static BackupScope ParseScope(string scope)
    {
        return Enum.TryParse<BackupScope>(scope, true, out var parsed) ? parsed : BackupScope.Full;
    }

    private static string BuildFileName(BackupScope scope, bool encrypted)
    {
        var suffix = encrypted ? ".hwbk" : ".zip";
        return $"HWInventory_{scope}_{DateTime.UtcNow:yyyyMMddHHmmss}{suffix}";
    }

    private string ResolveActor(bool automatic)
    {
        if (automatic)
        {
            return "system";
        }

        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
    }

    private static string? NormalizeRecipients(string? recipients)
    {
        if (string.IsNullOrWhiteSpace(recipients))
        {
            return null;
        }

        var normalized = string.Join(";", recipients
            .Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static byte[] Encrypt(byte[] data, string password)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        aes.Key = key.GetBytes(32);
        aes.GenerateIV();

        using var ms = new MemoryStream();
        ms.Write(new byte[] { (byte)'H', (byte)'W', (byte)'B', (byte)'K', 1 });
        ms.Write(BitConverter.GetBytes(salt.Length));
        ms.Write(salt);
        ms.Write(BitConverter.GetBytes(aes.IV.Length));
        ms.Write(aes.IV);
        using (var crypto = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
        {
            crypto.Write(data, 0, data.Length);
            crypto.FlushFinalBlock();
        }

        return ms.ToArray();
    }

    private static byte[] Decrypt(byte[] encrypted, string password)
    {
        using var ms = new MemoryStream(encrypted);
        Span<byte> header = stackalloc byte[5];
        if (ms.Read(header) != 5 || header[0] != 'H' || header[1] != 'W' || header[2] != 'B' || header[3] != 'K')
        {
            throw new InvalidOperationException("Neplatný formát zálohy.");
        }

        var saltLengthBuffer = new byte[sizeof(int)];
        ms.Read(saltLengthBuffer, 0, saltLengthBuffer.Length);
        var saltLength = BitConverter.ToInt32(saltLengthBuffer, 0);
        var salt = new byte[saltLength];
        ms.Read(salt, 0, salt.Length);

        var ivLengthBuffer = new byte[sizeof(int)];
        ms.Read(ivLengthBuffer, 0, ivLengthBuffer.Length);
        var ivLength = BitConverter.ToInt32(ivLengthBuffer, 0);
        var iv = new byte[ivLength];
        ms.Read(iv, 0, iv.Length);

        using var aes = Aes.Create();
        var key = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
        aes.Key = key.GetBytes(32);
        aes.IV = iv;

        using var crypto = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
        using var result = new MemoryStream();
        crypto.CopyTo(result);
        return result.ToArray();
    }

    private static string BuildStatus(BackupJob job)
    {
        return job.JobStatus.ToString();
    }

    private static BackupJobModel Map(BackupJob job)
    {
        return new BackupJobModel(
            job.Id,
            job.Scope.ToString(),
            job.FileName,
            job.StoragePath,
            job.EncryptionEnabled,
            job.SendEmail,
            job.IntegrityCheckEnabled,
            job.IsAutomatic,
            BuildStatus(job),
            job.FileSizeBytes,
            job.CompletedAtUtc,
            job.IntegrityPassed,
            job.CreatedAtUtc,
            job.CreatedBy ?? "system",
            job.FailureReason);
    }
}
