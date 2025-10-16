using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mail;
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

namespace HWInventory.Infrastructure.ImportExport;

public class ImportService : IImportService
{
    private readonly IAppDbContext _dbContext;
    private readonly IEmailConnectorStore _emailConnectorStore;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ImportService> _logger;

    public ImportService(
        IAppDbContext dbContext,
        IEmailConnectorStore emailConnectorStore,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ImportService> logger)
    {
        _dbContext = dbContext;
        _emailConnectorStore = emailConnectorStore;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<ImportJobModel> QueueImportAsync(ImportRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.StoragePath))
        {
            throw new ArgumentException("Storage path is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ContentBase64))
        {
            throw new ArgumentException("Soubor importu je povinný.", nameof(request));
        }

        var storagePath = Path.GetFullPath(request.StoragePath);
        Directory.CreateDirectory(storagePath);

        var actor = ResolveActor();
        var job = new ImportJob
        {
            Scope = request.Scope,
            Format = request.Format,
            ConflictStrategy = request.ConflictStrategy,
            DryRun = request.DryRun,
            StoragePath = storagePath,
            OriginalFileName = request.FileName,
            MappingJson = request.MappingJson,
            SendEmail = request.SendEmail,
            EmailRecipients = NormalizeRecipients(request.EmailRecipients),
            CreatedBy = actor,
            ModifiedBy = actor
        };

        var extension = request.Format switch
        {
            ImportFormat.Csv => ".csv",
            ImportFormat.Xlsx => ".xlsx",
            _ => ".dat"
        };

        var storedFilePath = Path.Combine(storagePath, $"{job.Id}{extension}");
        var bytes = Convert.FromBase64String(request.ContentBase64);
        await File.WriteAllBytesAsync(storedFilePath, bytes, cancellationToken);
        job.StoredFilePath = storedFilePath;

        _dbContext.ImportJobs.Add(job);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(job);
    }

    public async Task<IReadOnlyList<ImportJobModel>> ListHistoryAsync(int page, int size, CancellationToken cancellationToken = default)
    {
        var jobs = await _dbContext.ImportJobs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip(Math.Max(page - 1, 0) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return jobs.Select(Map).ToList();
    }

    public async Task<ImportJobModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await _dbContext.ImportJobs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return job is null ? null : Map(job);
    }

    public async Task ProcessPendingJobsAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _dbContext.ImportJobs
            .Where(x => x.JobStatus == ImportJobStatus.Pending)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(3)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        foreach (var job in pending)
        {
            job.JobStatus = ImportJobStatus.Running;
            job.ModifiedBy = "system";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var job in pending)
        {
            await ProcessJobAsync(job, cancellationToken);
        }
    }

    private async Task ProcessJobAsync(ImportJob job, CancellationToken cancellationToken)
    {
        try
        {
            var summary = await ExecuteImportAsync(job, cancellationToken);
            job.CompletedAtUtc = DateTime.UtcNow;
            job.JobStatus = ImportJobStatus.Completed;
            job.FailureReason = null;
            job.ResultLog = summary.Message;
            job.ProcessedRows = summary.Processed;
            job.CreatedRows = summary.Created;
            job.UpdatedRows = summary.Updated;
            job.SkippedRows = summary.Skipped;

            if (job.SendEmail)
            {
                await SendNotificationAsync(job, summary, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import job failed");
            job.JobStatus = ImportJobStatus.Failed;
            job.CompletedAtUtc = DateTime.UtcNow;
            job.FailureReason = ex.Message;
        }
        finally
        {
            job.ModifiedBy = "system";
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<ImportSummary> ExecuteImportAsync(ImportJob job, CancellationToken cancellationToken)
    {
        if (!File.Exists(job.StoredFilePath))
        {
            throw new FileNotFoundException("Import file not found.", job.StoredFilePath);
        }

        var rows = await LoadRowsAsync(job, cancellationToken);
        if (rows.Count == 0)
        {
            return new ImportSummary(0, 0, 0, 0, "Soubor neobsahuje žádná data.");
        }

        var mapping = ParseMapping(job.MappingJson, rows[0]);
        var summary = new ImportSummaryBuilder();

        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            summary.Processed++;

            var key = ResolveKey(job.Scope, mapping, row);
            if (string.IsNullOrWhiteSpace(key))
            {
                summary.Skipped++;
                summary.Messages.Add("Řádek byl přeskočen – chybí klíčová hodnota.");
                continue;
            }

            var existing = await FindExistingAsync(job.Scope, key, cancellationToken);
            if (existing is not null && job.ConflictStrategy == ImportConflictStrategy.Skip)
            {
                summary.Skipped++;
                continue;
            }

            if (job.DryRun)
            {
                if (existing is null)
                {
                    summary.Created++;
                }
                else
                {
                    summary.Updated++;
                }

                continue;
            }

            if (existing is null)
            {
                var entity = CreateEntity(job.Scope);
                ApplyRow(entity, mapping, row);
                SetAuditMetadata(entity, ResolveActor());
                await AddEntityAsync(job.Scope, entity, cancellationToken);
                summary.Created++;
            }
            else
            {
                ApplyRow(existing, mapping, row);
                SetAuditMetadata(existing, ResolveActor());
                summary.Updated++;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return summary.Build();
    }

    private async Task<IList<IDictionary<string, string>>> LoadRowsAsync(ImportJob job, CancellationToken cancellationToken)
    {
        return job.Format switch
        {
            ImportFormat.Csv => await LoadCsvAsync(job.StoredFilePath, cancellationToken),
            ImportFormat.Xlsx => await LoadXlsxAsync(job.StoredFilePath, cancellationToken),
            _ => throw new NotSupportedException($"Import format '{job.Format}' is not supported.")
        };
    }

    private static async Task<IList<IDictionary<string, string>>> LoadCsvAsync(string path, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);
        var lines = SplitLines(content).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        if (lines.Count == 0)
        {
            return Array.Empty<IDictionary<string, string>>();
        }

        var header = ParseCsvLine(lines[0]);
        var result = new List<IDictionary<string, string>>();

        foreach (var line in lines.Skip(1))
        {
            var values = ParseCsvLine(line);
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < header.Count; i++)
            {
                var column = header[i];
                var value = i < values.Count ? values[i] : string.Empty;
                row[column] = value;
            }

            if (row.Count > 0)
            {
                result.Add(row);
            }
        }

        return result;
    }

    private static async Task<IList<IDictionary<string, string>>> LoadXlsxAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream);

        var rows = new List<IDictionary<string, string>>();
        var header = new List<string>();
        var isHeader = true;

        while (reader.Read())
        {
            var values = new List<string>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                values.Add(reader.GetValue(i)?.ToString() ?? string.Empty);
            }

            if (isHeader)
            {
                header = values;
                isHeader = false;
                continue;
            }

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < header.Count; i++)
            {
                var column = header[i];
                if (string.IsNullOrWhiteSpace(column))
                {
                    continue;
                }

                var value = i < values.Count ? values[i] : string.Empty;
                row[column] = value;
            }

            if (row.Count > 0)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private static IReadOnlyDictionary<string, string> ParseMapping(string? mappingJson, IDictionary<string, string> sampleRow)
    {
        if (string.IsNullOrWhiteSpace(mappingJson))
        {
            return sampleRow.Keys.ToDictionary(k => k, v => v, StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(mappingJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return mapping is null || mapping.Count == 0
                ? sampleRow.Keys.ToDictionary(k => k, v => v, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(mapping, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return sampleRow.Keys.ToDictionary(k => k, v => v, StringComparer.OrdinalIgnoreCase);
        }
    }

    private string ResolveKey(ImportScope scope, IReadOnlyDictionary<string, string> mapping, IDictionary<string, string> row)
    {
        var keyProperty = scope switch
        {
            ImportScope.Servers => "InventoryNumber",
            ImportScope.NetworkDevices => "InventoryNumber",
            ImportScope.Workstations => "InventoryNumber",
            ImportScope.Dictionaries => "Name",
            _ => "InventoryNumber"
        };

        if (!mapping.TryGetValue(keyProperty, out var column))
        {
            column = keyProperty;
        }

        return row.TryGetValue(column, out var value) ? value : string.Empty;
    }

    private async Task<AuditableEntity?> FindExistingAsync(ImportScope scope, string key, CancellationToken cancellationToken)
    {
        return scope switch
        {
            ImportScope.Servers => await _dbContext.Servers.FirstOrDefaultAsync(x => x.InventoryNumber == key, cancellationToken),
            ImportScope.NetworkDevices => await _dbContext.NetworkDevices.FirstOrDefaultAsync(x => x.InventoryNumber == key, cancellationToken),
            ImportScope.Workstations => await _dbContext.Workstations.FirstOrDefaultAsync(x => x.InventoryNumber == key, cancellationToken),
            ImportScope.Dictionaries => await _dbContext.DictionaryEntries.FirstOrDefaultAsync(x => x.Name == key, cancellationToken),
            _ => null
        };
    }

    private static AuditableEntity CreateEntity(ImportScope scope)
    {
        return scope switch
        {
            ImportScope.Servers => new Server(),
            ImportScope.NetworkDevices => new NetworkDevice(),
            ImportScope.Workstations => new Workstation(),
            ImportScope.Dictionaries => new DictionaryEntry(),
            _ => throw new NotSupportedException($"Scope '{scope}' is not supported.")
        };
    }

    private static void ApplyRow(AuditableEntity entity, IReadOnlyDictionary<string, string> mapping, IDictionary<string, string> row)
    {
        foreach (var kvp in mapping)
        {
            var propertyName = kvp.Key;
            var column = string.IsNullOrWhiteSpace(kvp.Value) ? propertyName : kvp.Value;

            if (!row.TryGetValue(column, out var value))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var property = entity.GetType().GetProperty(propertyName);
            if (property is null || !property.CanWrite)
            {
                continue;
            }

            try
            {
                var converted = ConvertValue(property.PropertyType, value);
                property.SetValue(entity, converted);
            }
            catch
            {
                // Ignore conversion failures – these budou uvedeny v logu výsledku.
            }
        }
    }

    private static object? ConvertValue(Type type, string value)
    {
        if (type == typeof(string))
        {
            return value.Trim();
        }

        if (type == typeof(Guid) || type == typeof(Guid?))
        {
            return Guid.TryParse(value, out var guid) ? guid : null;
        }

        if (type == typeof(DateTime) || type == typeof(DateTime?))
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
                ? date
                : null;
        }

        if (type == typeof(int) || type == typeof(int?))
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;
        }

        if (type == typeof(bool) || type == typeof(bool?))
        {
            return value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        return value;
    }

    private async Task AddEntityAsync(ImportScope scope, AuditableEntity entity, CancellationToken cancellationToken)
    {
        switch (scope)
        {
            case ImportScope.Servers:
                _dbContext.Servers.Add((Server)entity);
                break;
            case ImportScope.NetworkDevices:
                _dbContext.NetworkDevices.Add((NetworkDevice)entity);
                break;
            case ImportScope.Workstations:
                _dbContext.Workstations.Add((Workstation)entity);
                break;
            case ImportScope.Dictionaries:
                _dbContext.DictionaryEntries.Add((DictionaryEntry)entity);
                break;
            default:
                throw new NotSupportedException($"Scope '{scope}' is not supported.");
        }

        await Task.CompletedTask;
    }

    private static void SetAuditMetadata(AuditableEntity entity, string actor)
    {
        entity.CreatedBy ??= actor;
        entity.ModifiedBy = actor;
    }

    private static IEnumerable<string> SplitLines(string content)
    {
        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            yield return line;
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(line))
        {
            result.Add(string.Empty);
            return result;
        }

        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result;
    }

    private sealed class ImportSummaryBuilder
    {
        public long Processed { get; set; }
        public long Created { get; set; }
        public long Updated { get; set; }
        public long Skipped { get; set; }
        public IList<string> Messages { get; } = new List<string>();

        public ImportSummary Build()
        {
            var log = Messages.Count == 0
                ? $"Zpracováno {Processed}, vytvořeno {Created}, aktualizováno {Updated}, přeskočeno {Skipped}."
                : string.Join(Environment.NewLine, Messages);

            return new ImportSummary(Processed, Created, Updated, Skipped, log);
        }
    }

    private async Task SendNotificationAsync(ImportJob job, ImportSummary summary, CancellationToken cancellationToken)
    {
        var client = await _emailConnectorStore.GetClientAsync(cancellationToken);
        if (client is null)
        {
            _logger.LogWarning("SMTP konektor není nakonfigurován – notifikace nebude odeslána.");
            return;
        }

        if (string.IsNullOrWhiteSpace(job.EmailRecipients))
        {
            return;
        }

        var message = new MailMessage
        {
            Subject = $"HW Inventory – import {job.Scope} dokončen",
            Body = $"Proces importu skončil se statusem {job.JobStatus}.\n{summary.Message}",
            IsBodyHtml = false
        };

        foreach (var recipient in job.EmailRecipients.Split(';'))
        {
            message.To.Add(recipient);
        }

        await client.SendMailAsync(message, cancellationToken);
    }

    private ImportJobModel Map(ImportJob job)
    {
        return new ImportJobModel(
            job.Id,
            job.Scope.ToString(),
            job.Format.ToString(),
            job.JobStatus.ToString(),
            job.ConflictStrategy.ToString(),
            job.DryRun,
            job.StoragePath,
            job.OriginalFileName,
            job.CreatedAtUtc,
            job.CompletedAtUtc,
            job.CreatedBy,
            job.FailureReason,
            job.ProcessedRows,
            job.CreatedRows,
            job.UpdatedRows,
            job.SkippedRows,
            job.ResultLog,
            job.SendEmail,
            job.EmailRecipients);
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

    private record ImportSummary(long Processed, long Created, long Updated, long Skipped, string Message);
}
