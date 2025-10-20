using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HWInventory.Infrastructure.Reports;

public class ReportService : IReportService
{
    private readonly IAppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ReportService> _logger;
    private readonly IEmailConnectorStore _emailConnectorStore;

    public ReportService(
        IAppDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ReportService> logger,
        IEmailConnectorStore emailConnectorStore)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _emailConnectorStore = emailConnectorStore;
    }

    public async Task<ReportDefinitionModel> CreateAsync(CreateReportRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.StoragePath))
        {
            throw new ArgumentException("Storage path is required", nameof(request.StoragePath));
        }

        var definition = new ReportDefinition
        {
            Name = request.Name,
            Description = request.Description,
            Scope = request.Scope,
            Format = request.Format,
            Recurrence = request.Recurrence,
            FilterJson = request.FilterJson,
            Recipients = NormalizeRecipients(request.Recipients),
            StoragePath = Path.GetFullPath(request.StoragePath),
            RunAtTime = request.RunAtTime,
            RunOnDayOfWeek = request.RunOnDayOfWeek,
            RunOnDayOfMonth = request.RunOnDayOfMonth,
            Enabled = request.Enabled,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = ResolveActor(),
            ModifiedBy = ResolveActor()
        };

        definition.NextRunAtUtc = CalculateNextRun(definition, DateTimeOffset.UtcNow);

        _dbContext.ReportDefinitions.Add(definition);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Map(definition);
    }

    public async Task<ReportDefinitionModel> UpdateAsync(Guid id, UpdateReportRequest request, CancellationToken cancellationToken = default)
    {
        var definition = await _dbContext.ReportDefinitions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (definition is null)
        {
            throw new InvalidOperationException("Report definition not found");
        }

        definition.Name = request.Name;
        definition.Description = request.Description;
        definition.Scope = request.Scope;
        definition.Format = request.Format;
        definition.Recurrence = request.Recurrence;
        definition.FilterJson = request.FilterJson;
        definition.Recipients = NormalizeRecipients(request.Recipients);
        definition.StoragePath = Path.GetFullPath(request.StoragePath);
        definition.RunAtTime = request.RunAtTime;
        definition.RunOnDayOfWeek = request.RunOnDayOfWeek;
        definition.RunOnDayOfMonth = request.RunOnDayOfMonth;
        definition.Enabled = request.Enabled;
        definition.ModifiedAtUtc = DateTime.UtcNow;
        definition.ModifiedBy = ResolveActor();
        definition.NextRunAtUtc = CalculateNextRun(definition, DateTimeOffset.UtcNow);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(definition);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var definition = await _dbContext.ReportDefinitions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (definition is null)
        {
            return;
        }

        _dbContext.ReportDefinitions.Remove(definition);
        await _dbContext.SaveChangesAsync(cancellationToken);

    }

    public async Task<ReportDefinitionModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var definition = await _dbContext.ReportDefinitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return definition is null ? null : Map(definition);
    }

    public async Task<IReadOnlyList<ReportDefinitionModel>> ListAsync(int page, int size, CancellationToken cancellationToken = default)
    {
        var items = await _dbContext.ReportDefinitions
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Skip(Math.Max(page - 1, 0) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return items.Select(Map).ToList();
    }

    public async Task<ReportRunModel> TriggerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var definition = await _dbContext.ReportDefinitions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (definition is null)
        {
            throw new InvalidOperationException("Report definition not found");
        }

        var run = new ReportRun
        {
            ReportDefinitionId = definition.Id,
            Status = ReportRunStatus.Running,
            StartedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = ResolveActor(),
            ModifiedBy = ResolveActor()
        };

        _dbContext.ReportRuns.Add(run);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await ExecuteReportAsync(definition, run, cancellationToken);
        return Map(run);
    }

    public async Task<IReadOnlyList<ReportRunModel>> ListRunsAsync(Guid id, int page, int size, CancellationToken cancellationToken = default)
    {
        var runs = await _dbContext.ReportRuns
            .AsNoTracking()
            .Where(x => x.ReportDefinitionId == id)
            .OrderByDescending(x => x.StartedAtUtc)
            .Skip(Math.Max(page - 1, 0) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return runs.Select(Map).ToList();
    }

    public async Task<ReportRunModel?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await _dbContext.ReportRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == runId, cancellationToken);
        return run is null ? null : Map(run);
    }

    public async Task<byte[]?> GetArtifactAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await _dbContext.ReportRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == runId, cancellationToken);
        if (run?.ArtifactPath is null || !File.Exists(run.ArtifactPath))
        {
            return null;
        }

        return await File.ReadAllBytesAsync(run.ArtifactPath, cancellationToken);
    }

    public async Task ProcessDueReportsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var dueDefinitions = await _dbContext.ReportDefinitions
            .Where(x => x.Enabled && x.Recurrence != ReportRecurrence.Manual && x.NextRunAtUtc != null && x.NextRunAtUtc <= now)
            .ToListAsync(cancellationToken);

        if (dueDefinitions.Count == 0)
        {
            return;
        }

        foreach (var definition in dueDefinitions)
        {
            var run = new ReportRun
            {
                ReportDefinitionId = definition.Id,
                Status = ReportRunStatus.Running,
                StartedAtUtc = DateTimeOffset.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                CreatedBy = "system",
                ModifiedBy = "system"
            };

            _dbContext.ReportRuns.Add(run);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await ExecuteReportAsync(definition, run, cancellationToken);
        }
    }

    private async Task ExecuteReportAsync(ReportDefinition definition, ReportRun run, CancellationToken cancellationToken)
    {
        try
        {
            var storagePath = definition.StoragePath ?? Path.Combine(AppContext.BaseDirectory, "reports");
            Directory.CreateDirectory(storagePath);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            var extension = definition.Format switch
            {
                ExportFormat.Csv => ".csv",
                ExportFormat.Xlsx => ".xlsx",
                ExportFormat.Pdf => ".pdf",
                _ => ".dat"
            };
            var fileName = $"report_{definition.Scope.ToString().ToLowerInvariant()}_{timestamp}{extension}";
            var filePath = Path.Combine(storagePath, fileName);

            switch (definition.Format)
            {
                case ExportFormat.Csv:
                    await GenerateCsvAsync(definition.Scope, filePath, cancellationToken);
                    break;
                case ExportFormat.Xlsx:
                    await GenerateCsvAsync(definition.Scope, filePath, cancellationToken);
                    break;
                case ExportFormat.Pdf:
                    await GenerateCsvAsync(definition.Scope, filePath, cancellationToken);
                    break;
                default:
                    await GenerateCsvAsync(definition.Scope, filePath, cancellationToken);
                    break;
            }

            run.Status = ReportRunStatus.Completed;
            run.CompletedAtUtc = DateTimeOffset.UtcNow;
            run.ArtifactPath = filePath;
            run.ModifiedAtUtc = DateTime.UtcNow;
            run.ModifiedBy = "system";
            definition.LastRunAtUtc = run.CompletedAtUtc;
            definition.NextRunAtUtc = CalculateNextRun(definition, run.CompletedAtUtc ?? DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Report generation failed for {Report}", definition.Name);
            run.Status = ReportRunStatus.Failed;
            run.CompletedAtUtc = DateTimeOffset.UtcNow;
            run.FailureReason = ex.Message;
            run.ModifiedAtUtc = DateTime.UtcNow;
            run.ModifiedBy = "system";
            definition.NextRunAtUtc = CalculateNextRun(definition, DateTimeOffset.UtcNow.AddHours(1));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await NotifySubscribersAsync(definition, run, cancellationToken);
    }

    private async Task GenerateCsvAsync(ReportScope scope, string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var writer = new StreamWriter(stream, Encoding.UTF8);

        switch (scope)
        {
            case ReportScope.Servers:
                await WriteServersAsync(writer, cancellationToken);
                break;
            case ReportScope.NetworkDevices:
                await WriteNetworkDevicesAsync(writer, cancellationToken);
                break;
            case ReportScope.Workstations:
                await WriteWorkstationsAsync(writer, cancellationToken);
                break;
            case ReportScope.Audit:
                await WriteAuditAsync(writer, cancellationToken);
                break;
            default:
                await WriteServersAsync(writer, cancellationToken);
                break;
        }
    }

    private async Task WriteServersAsync(StreamWriter writer, CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync("Name,InventoryNumber,Manufacturer,Model,Environment,WsusPriority,OperatingSystem,Role,Location,Status");
        var servers = await _dbContext.Servers.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

        var dictionaryIds = servers
            .SelectMany(server => new[]
            {
                server.EnvironmentId,
                server.WsusPriorityId,
                server.OperatingSystemId,
                server.ServerRoleId,
                server.LocationId
            })
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var dictionaryValues = await LoadDictionaryValuesAsync(dictionaryIds, cancellationToken);

        foreach (var server in servers)
        {
            var environment = TryResolve(dictionaryValues, server.EnvironmentId);
            var wsusPriority = TryResolve(dictionaryValues, server.WsusPriorityId);
            var operatingSystem = TryResolve(dictionaryValues, server.OperatingSystemId);
            var serverRole = TryResolve(dictionaryValues, server.ServerRoleId);
            var location = TryResolve(dictionaryValues, server.LocationId);

            await writer.WriteLineAsync(string.Join(',', new[]
            {
                Escape(server.Name),
                Escape(server.InventoryNumber),
                Escape(server.Manufacturer),
                Escape(server.Model),
                Escape(environment),
                Escape(wsusPriority),
                Escape(operatingSystem),
                Escape(serverRole),
                Escape(location),
                server.Status.ToString()
            }));
        }
    }

    private async Task WriteNetworkDevicesAsync(StreamWriter writer, CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync("Name,InventoryNumber,Type,Manufacturer,Model,Location,Status");
        var devices = await _dbContext.NetworkDevices.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

        var dictionaryIds = devices
            .SelectMany(device => new[] { device.DeviceTypeId, device.LocationId })
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var dictionaryValues = await LoadDictionaryValuesAsync(dictionaryIds, cancellationToken);

        foreach (var device in devices)
        {
            var deviceType = TryResolve(dictionaryValues, device.DeviceTypeId);
            var location = TryResolve(dictionaryValues, device.LocationId);

            await writer.WriteLineAsync(string.Join(',', new[]
            {
                Escape(device.Name),
                Escape(device.InventoryNumber),
                Escape(deviceType),
                Escape(device.Manufacturer),
                Escape(device.Model),
                Escape(location),
                device.Status.ToString()
            }));
        }
    }

    private async Task WriteWorkstationsAsync(StreamWriter writer, CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync("Name,InventoryNumber,Owner,Department,OperatingSystem,Type,Location,Status");
        var workstations = await _dbContext.Workstations.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

        var dictionaryIds = workstations
            .SelectMany(workstation => new[]
            {
                workstation.OperatingSystemId,
                workstation.WorkstationTypeId,
                workstation.LocationId
            })
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        var dictionaryValues = await LoadDictionaryValuesAsync(dictionaryIds, cancellationToken);

        foreach (var workstation in workstations)
        {
            var operatingSystem = TryResolve(dictionaryValues, workstation.OperatingSystemId);
            var workstationType = TryResolve(dictionaryValues, workstation.WorkstationTypeId);
            var location = TryResolve(dictionaryValues, workstation.LocationId);

            await writer.WriteLineAsync(string.Join(',', new[]
            {
                Escape(workstation.Name),
                Escape(workstation.InventoryNumber),
                Escape(workstation.OwnerDisplayName),
                Escape(workstation.OwnerDepartment),
                Escape(operatingSystem),
                Escape(workstationType),
                Escape(location),
                workstation.Status.ToString()
            }));
        }
    }

    private async Task WriteAuditAsync(StreamWriter writer, CancellationToken cancellationToken)
    {
        await writer.WriteLineAsync("Timestamp,Entity,EntityId,Action,PerformedBy");
        var logs = await _dbContext.AuditLogs.AsNoTracking().OrderByDescending(x => x.PerformedAtUtc).Take(500).ToListAsync(cancellationToken);
        foreach (var log in logs)
        {
            await writer.WriteLineAsync(string.Join(',', new[]
            {
                Escape(log.PerformedAtUtc.ToString("u", CultureInfo.InvariantCulture)),
                Escape(log.EntityType),
                Escape(log.EntityId.ToString()),
                Escape(log.Action ?? string.Empty),
                Escape(log.PerformedBy ?? string.Empty)
            }));
        }
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private async Task<Dictionary<Guid, string>> LoadDictionaryValuesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var uniqueIds = ids.Where(id => id != Guid.Empty).Distinct().ToList();
        if (uniqueIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await _dbContext.DictionaryEntries
            .AsNoTracking()
            .Where(entry => uniqueIds.Contains(entry.Id))
            .ToDictionaryAsync(entry => entry.Id, entry => entry.Value, cancellationToken);
    }

    private static string TryResolve(IReadOnlyDictionary<Guid, string> source, Guid key)
    {
        return key != Guid.Empty && source.TryGetValue(key, out var value)
            ? value
            : key == Guid.Empty ? string.Empty : key.ToString();
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

    private async Task NotifySubscribersAsync(ReportDefinition definition, ReportRun run, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(definition.Recipients))
        {
            return;
        }

        var recipients = definition.Recipients
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .ToList();

        if (recipients.Count == 0)
        {
            return;
        }

        var statusText = run.Status == ReportRunStatus.Completed
            ? "dokončen"
            : run.Status == ReportRunStatus.Failed
                ? "selhal"
                : run.Status.ToString();

        var builder = new StringBuilder();
        builder.AppendLine($"Report '{definition.Name}' ({definition.Scope}) byl {statusText} v {DateTimeOffset.UtcNow:u}.");

        if (run.Status == ReportRunStatus.Completed && !string.IsNullOrWhiteSpace(run.ArtifactPath))
        {
            builder.AppendLine($"Soubor je uložen na: {run.ArtifactPath}");
        }

        if (run.Status == ReportRunStatus.Failed && !string.IsNullOrWhiteSpace(run.FailureReason))
        {
            builder.AppendLine($"Důvod selhání: {run.FailureReason}");
        }

        builder.AppendLine();
        builder.AppendLine("Pro detailní přehled otevřete HW Inventory a přejděte do sekce Reporty.");

        var result = await _emailConnectorStore.SendNotificationAsync(
            new EmailNotificationRequest(
                recipients,
                $"HW Inventory – report {definition.Name}",
                builder.ToString(),
                false),
            cancellationToken);

        if (!result.Success)
        {
            _logger.LogWarning("Odeslání notifikace reportu {ReportName} selhalo: {Message}", definition.Name, result.Message);
        }
    }

    private DateTimeOffset? CalculateNextRun(ReportDefinition definition, DateTimeOffset reference)
    {
        if (!definition.Enabled || definition.Recurrence == ReportRecurrence.Manual)
        {
            return null;
        }

        var runTime = definition.RunAtTime ?? TimeSpan.FromHours(6);
        var candidate = new DateTimeOffset(reference.Date, TimeSpan.Zero).Add(runTime);
        if (candidate <= reference)
        {
            candidate = candidate.AddDays(1);
        }

        switch (definition.Recurrence)
        {
            case ReportRecurrence.Daily:
                return candidate;
            case ReportRecurrence.Weekly:
                var targetDay = definition.RunOnDayOfWeek ?? DayOfWeek.Monday;
                while (candidate.DayOfWeek != targetDay)
                {
                    candidate = candidate.AddDays(1);
                }
                return candidate;
            case ReportRecurrence.Monthly:
                var day = Math.Clamp(definition.RunOnDayOfMonth ?? 1, 1, 28);
                var nextMonth = new DateTime(reference.Year, reference.Month, 1, runTime.Hours, runTime.Minutes, runTime.Seconds, DateTimeKind.Utc);
                if (reference.Day >= day)
                {
                    nextMonth = nextMonth.AddMonths(1);
                }
                var scheduled = new DateTime(nextMonth.Year, nextMonth.Month, day, runTime.Hours, runTime.Minutes, runTime.Seconds, DateTimeKind.Utc);
                return new DateTimeOffset(scheduled);
            default:
                return null;
        }
    }

    private string ResolveActor()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
    }

    private static ReportDefinitionModel Map(ReportDefinition definition)
    {
        return new ReportDefinitionModel(
            definition.Id,
            definition.Name,
            definition.Description,
            definition.Scope,
            definition.Format,
            definition.Recurrence,
            definition.FilterJson,
            definition.Recipients,
            definition.StoragePath ?? string.Empty,
            definition.RunAtTime,
            definition.RunOnDayOfWeek,
            definition.RunOnDayOfMonth,
            definition.Enabled,
            definition.NextRunAtUtc,
            definition.LastRunAtUtc,
            definition.CreatedAtUtc,
            definition.ModifiedAtUtc,
            definition.CreatedBy,
            definition.ModifiedBy);
    }

    private static ReportRunModel Map(ReportRun run)
    {
        return new ReportRunModel(
            run.Id,
            run.ReportDefinitionId,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.Status,
            run.ArtifactPath,
            run.FailureReason);
    }
}
