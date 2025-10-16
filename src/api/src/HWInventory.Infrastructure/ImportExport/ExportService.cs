using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HWInventory.Infrastructure.ImportExport;

public class ExportService : IExportService
{
    private readonly IAppDbContext _dbContext;
    private readonly IEmailConnectorStore _emailConnectorStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ExportService> _logger;

    public ExportService(
        IAppDbContext dbContext,
        IEmailConnectorStore emailConnectorStore,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ExportService> logger)
    {
        _dbContext = dbContext;
        _emailConnectorStore = emailConnectorStore;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<ExportJobModel> QueueExportAsync(ExportRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.StoragePath))
        {
            throw new ArgumentException("Storage path is required.", nameof(request));
        }

        var storagePath = Path.GetFullPath(request.StoragePath);
        Directory.CreateDirectory(storagePath);

        var actor = ResolveActor();

        var job = new ExportJob
        {
            Scope = request.Scope,
            Format = request.Format,
            StoragePath = storagePath,
            FileName = BuildFileName(request.Scope, request.Format),
            FilterJson = request.FilterJson,
            SendEmail = request.SendEmail,
            EmailRecipients = NormalizeRecipients(request.EmailRecipients),
            CreatedBy = actor,
            ModifiedBy = actor,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        };

        _dbContext.ExportJobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(job);
    }

    public async Task<IReadOnlyList<ExportJobModel>> ListHistoryAsync(int page, int size, CancellationToken cancellationToken = default)
    {
        var jobs = await _dbContext.ExportJobs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(Math.Max(page - 1, 0) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return jobs.Select(Map).ToList();
    }

    public async Task<ExportJobModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.ExportJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return job is null ? null : Map(job);
    }

    public async Task<ExportJobModel?> RegisterDownloadAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.ExportJobs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (job is null)
        {
            return null;
        }

        if (job.JobStatus != ExportJobStatus.Completed)
        {
            return null;
        }

        if (job.ExpiresAtUtc.HasValue && job.ExpiresAtUtc.Value < DateTime.UtcNow)
        {
            return null;
        }

        job.DownloadCount += 1;
        job.LastDownloadedAtUtc = DateTime.UtcNow;
        job.ModifiedBy = actor;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(job);
    }

    public async Task ProcessPendingJobsAsync(CancellationToken cancellationToken = default)
    {
        var pendingJobs = await _dbContext.ExportJobs
            .Where(x => x.JobStatus == ExportJobStatus.Pending)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(5)
            .ToListAsync(cancellationToken);

        foreach (var job in pendingJobs)
        {
            job.JobStatus = ExportJobStatus.Running;
            job.ModifiedBy = "system";
        }

        if (pendingJobs.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var job in pendingJobs)
        {
            await ProcessJobAsync(job, cancellationToken);
        }

        var expirationThreshold = DateTime.UtcNow;
        var expiredJobs = await _dbContext.ExportJobs
            .Where(x => x.JobStatus == ExportJobStatus.Completed && x.ExpiresAtUtc < expirationThreshold)
            .ToListAsync(cancellationToken);

        if (expiredJobs.Count > 0)
        {
            foreach (var job in expiredJobs)
            {
                job.JobStatus = ExportJobStatus.Expired;
                job.ModifiedBy = "system";
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ProcessJobAsync(ExportJob job, CancellationToken cancellationToken)
    {
        try
        {
            var artifactPath = await GenerateExportAsync(job, cancellationToken);
            job.ArtifactPath = artifactPath;
            job.CompletedAtUtc = DateTime.UtcNow;
            job.FileSizeBytes = File.Exists(artifactPath) ? new FileInfo(artifactPath).Length : null;
            job.JobStatus = ExportJobStatus.Completed;
            job.FailureReason = null;

            if (job.SendEmail)
            {
                await SendNotificationAsync(job, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zpracování exportu selhalo");
            job.JobStatus = ExportJobStatus.Failed;
            job.CompletedAtUtc = DateTime.UtcNow;
            job.FailureReason = ex.Message;
        }
        finally
        {
            job.ModifiedBy = "system";
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<string> GenerateExportAsync(ExportJob job, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(job.StoragePath, job.FileName);
        Directory.CreateDirectory(job.StoragePath);

        switch (job.Scope)
        {
            case ExportScope.Servers:
                await ExportServersAsync(fullPath, cancellationToken);
                break;
            case ExportScope.NetworkDevices:
                await ExportNetworkDevicesAsync(fullPath, cancellationToken);
                break;
            case ExportScope.Workstations:
                await ExportWorkstationsAsync(fullPath, cancellationToken);
                break;
            case ExportScope.Dictionaries:
                await ExportDictionariesAsync(fullPath, cancellationToken);
                break;
            case ExportScope.AuditLogs:
                await ExportAuditLogsAsync(fullPath, cancellationToken);
                break;
            default:
                throw new InvalidOperationException($"Export scope {job.Scope} není podporován.");
        }

        return fullPath;
    }

    private async Task ExportServersAsync(string path, CancellationToken cancellationToken)
    {
        var servers = await _dbContext.Servers
            .AsNoTracking()
            .Include(x => x.NetworkAssignments)
            .ToListAsync(cancellationToken);

        var dictionaryLookup = await LoadDictionaryLookupAsync(servers.SelectMany(s => new[]
        {
            s.EnvironmentId,
            s.WsusPriorityId,
            s.OperatingSystemId,
            s.ServerRoleId,
            s.LocationId
        }), cancellationToken);

        await using var writer = CreateWriter(path);
        await writer.WriteLineAsync("Name,InventoryNumber,Manufacturer,Model,Environment,WsusPriority,OperatingSystem,ServerRole,Location,RackPosition,PurchasedAt,SupportUntil,Status,NetworkAssignments");

        foreach (var server in servers)
        {
            var assignments = string.Join(" | ", server.NetworkAssignments.Select(a =>
                $"{a.Label}:{ResolveDictionaryName(dictionaryLookup, a.VlanId)}:{a.IpAddress}"));

            await WriteCsvLineAsync(writer,
                server.Name,
                server.InventoryNumber,
                server.Manufacturer,
                server.Model,
                ResolveDictionaryName(dictionaryLookup, server.EnvironmentId),
                ResolveDictionaryName(dictionaryLookup, server.WsusPriorityId),
                ResolveDictionaryName(dictionaryLookup, server.OperatingSystemId),
                ResolveDictionaryName(dictionaryLookup, server.ServerRoleId),
                ResolveDictionaryName(dictionaryLookup, server.LocationId),
                server.RackPosition,
                FormatDate(server.PurchasedAt),
                FormatDate(server.SupportUntil),
                server.Status.ToString(),
                assignments);
        }
    }

    private async Task ExportNetworkDevicesAsync(string path, CancellationToken cancellationToken)
    {
        var devices = await _dbContext.NetworkDevices
            .AsNoTracking()
            .Include(x => x.NetworkAssignments)
            .ToListAsync(cancellationToken);

        var dictionaryLookup = await LoadDictionaryLookupAsync(devices.SelectMany(device => new[]
        {
            device.DeviceTypeId,
            device.LocationId
        }), cancellationToken);

        await using var writer = CreateWriter(path);
        await writer.WriteLineAsync("Name,InventoryNumber,DeviceType,Manufacturer,Model,Location,RackPosition,SupportUntil,Status,NetworkAssignments");

        foreach (var device in devices)
        {
            var assignments = string.Join(" | ", device.NetworkAssignments.Select(a =>
                $"{a.Label}:{ResolveDictionaryName(dictionaryLookup, a.VlanId)}:{a.IpAddress}"));

            await WriteCsvLineAsync(writer,
                device.Name,
                device.InventoryNumber,
                ResolveDictionaryName(dictionaryLookup, device.DeviceTypeId),
                device.Manufacturer,
                device.Model,
                ResolveDictionaryName(dictionaryLookup, device.LocationId),
                device.RackPosition,
                FormatDate(device.SupportUntil),
                device.Status.ToString(),
                assignments);
        }
    }

    private async Task ExportWorkstationsAsync(string path, CancellationToken cancellationToken)
    {
        var workstations = await _dbContext.Workstations
            .AsNoTracking()
            .Include(x => x.NetworkAssignments)
            .ToListAsync(cancellationToken);

        var dictionaryLookup = await LoadDictionaryLookupAsync(workstations.SelectMany(ws => new[]
        {
            ws.OperatingSystemId,
            ws.WorkstationTypeId,
            ws.LocationId
        }), cancellationToken);

        await using var writer = CreateWriter(path);
        await writer.WriteLineAsync("Name,InventoryNumber,OperatingSystem,WorkstationType,Owner,OwnerDepartment,Location,Cpu,Ram,Storage,MacAddress,PurchasedAt,SupportUntil,Status,NetworkAssignments");

        foreach (var workstation in workstations)
        {
            var assignments = string.Join(" | ", workstation.NetworkAssignments.Select(a =>
                $"{a.Label}:{ResolveDictionaryName(dictionaryLookup, a.VlanId)}:{a.IpAddress}"));

            await WriteCsvLineAsync(writer,
                workstation.Name,
                workstation.InventoryNumber,
                ResolveDictionaryName(dictionaryLookup, workstation.OperatingSystemId),
                ResolveDictionaryName(dictionaryLookup, workstation.WorkstationTypeId),
                workstation.OwnerDisplayName,
                workstation.OwnerDepartment,
                ResolveDictionaryName(dictionaryLookup, workstation.LocationId),
                workstation.Cpu,
                workstation.Ram,
                workstation.Storage,
                workstation.MacAddress,
                FormatDate(workstation.PurchasedAt),
                FormatDate(workstation.SupportUntil),
                workstation.Status.ToString(),
                assignments);
        }
    }

    private async Task ExportDictionariesAsync(string path, CancellationToken cancellationToken)
    {
        var dictionaries = await _dbContext.DictionaryEntries
            .AsNoTracking()
            .OrderBy(x => x.DictType)
            .ThenBy(x => x.Key)
            .ToListAsync(cancellationToken);

        await using var writer = CreateWriter(path);
        await writer.WriteLineAsync("DictType,Key,Value,Description,Status");

        foreach (var entry in dictionaries)
        {
            await WriteCsvLineAsync(writer,
                entry.DictType,
                entry.Key,
                entry.Value,
                entry.Description,
                entry.Status.ToString());
        }
    }

    private async Task ExportAuditLogsAsync(string path, CancellationToken cancellationToken)
    {
        var logs = await _dbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.PerformedAtUtc)
            .Take(5000)
            .ToListAsync(cancellationToken);

        await using var writer = CreateWriter(path);
        await writer.WriteLineAsync("PerformedAtUtc,EntityType,EntityId,Action,PerformedBy,Roles,Summary");

        foreach (var log in logs)
        {
            await WriteCsvLineAsync(writer,
                log.PerformedAtUtc.ToString("o", CultureInfo.InvariantCulture),
                log.EntityType,
                log.EntityId.ToString(),
                log.Action,
                log.PerformedBy,
                log.Roles,
                log.ChangeSummary);
        }
    }

    private async Task<Dictionary<Guid, string>> LoadDictionaryLookupAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var uniqueIds = ids.Where(id => id != Guid.Empty).Distinct().ToList();
        if (uniqueIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var entries = await _dbContext.DictionaryEntries
            .AsNoTracking()
            .Where(x => uniqueIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Value })
            .ToListAsync(cancellationToken);

        return entries.ToDictionary(x => x.Id, x => x.Value);
    }

    private static StreamWriter CreateWriter(string path)
    {
        return new StreamWriter(path, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static Task WriteCsvLineAsync(StreamWriter writer, params string?[] values)
    {
        var escaped = values.Select(Escape).ToArray();
        return writer.WriteLineAsync(string.Join(',', escaped));
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var normalized = value.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{normalized}\"" : normalized;
    }

    private static string? ResolveDictionaryName(Dictionary<Guid, string> lookup, Guid? id)
    {
        if (!id.HasValue)
        {
            return null;
        }

        return lookup.TryGetValue(id.Value, out var value) ? value : id.Value.ToString();
    }

    private static string? FormatDate(DateTime? date)
    {
        return date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private async Task SendNotificationAsync(ExportJob job, CancellationToken cancellationToken)
    {
        var connector = await _emailConnectorStore.GetAsync(cancellationToken);
        if (connector is null || !connector.Enabled)
        {
            _logger.LogWarning("SMTP konektor není nastaven, notifikace nebude odeslána.");
            return;
        }

        if (string.IsNullOrWhiteSpace(connector.Host) || string.IsNullOrWhiteSpace(job.EmailRecipients))
        {
            return;
        }

        var recipients = job.EmailRecipients
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (recipients.Length == 0)
        {
            return;
        }

        var password = connector.HasPassword
            ? await _dbContext.ConnectorProfiles
                .Include(x => x.Secrets)
                .Where(x => x.Type == "SMTP")
                .SelectMany(x => x.Secrets)
                .Select(x => x.SecretReference)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        using var client = new SmtpClient(connector.Host, connector.Port ?? 25)
        {
            EnableSsl = connector.UseSsl
        };

        if (!string.IsNullOrWhiteSpace(connector.Username))
        {
            client.Credentials = new System.Net.NetworkCredential(connector.Username, password);
        }

        var message = new MailMessage
        {
            From = new MailAddress(connector.FromAddress ?? "noreply@example.com"),
            Subject = $"HW Inventory export dokončen ({job.Scope})",
            Body = $"Export {job.Scope} byl dokončen v {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC. Soubor: {job.FileName}",
            IsBodyHtml = false
        };

        foreach (var recipient in recipients)
        {
            message.To.Add(recipient);
        }

        await client.SendMailAsync(message, cancellationToken);
    }

    private ExportJobModel Map(ExportJob job)
    {
        return new ExportJobModel(
            job.Id,
            job.Scope.ToString(),
            job.Format.ToString(),
            job.StoragePath,
            job.FileName,
            job.JobStatus.ToString(),
            job.FileSizeBytes,
            job.CreatedAtUtc,
            job.CompletedAtUtc,
            job.ExpiresAtUtc,
            job.CreatedBy,
            job.FailureReason,
            job.SendEmail,
            job.EmailRecipients,
            job.ArtifactPath,
            job.DownloadCount,
            job.LastDownloadedAtUtc);
    }

    private string ResolveActor()
    {
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

    private static string BuildFileName(ExportScope scope, ExportFormat format)
    {
        var extension = format switch
        {
            ExportFormat.Csv => ".csv",
            ExportFormat.Xlsx => ".xlsx",
            _ => ".dat"
        };

        return $"HWInventory_{scope}_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
    }
}
