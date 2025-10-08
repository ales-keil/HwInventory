using System;
using System.Collections.Generic;
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

        if (job.Format == ImportFormat.Xlsx)
        {
            // XLSX podpora bude doplněna později.
            return new ImportSummary(0, 0, 0, 0, "XLSX import zatím není implementován – soubor byl archivován bez zpracování.");
        }

        var lines = await File.ReadAllLinesAsync(job.StoredFilePath, Encoding.UTF8, cancellationToken);
        if (lines.Length == 0)
        {
            return new ImportSummary(0, 0, 0, 0, "Soubor je prázdný.");
        }

        var processed = Math.Max(lines.Length - 1, 0);
        if (job.DryRun)
        {
            return new ImportSummary(processed, 0, 0, processed, $"Dry-run dokončen. Zpracováno {processed} řádků (bez zápisu).");
        }

        // Skutečné zpracování dat bude doplněno v dalších iteracích.
        return new ImportSummary(processed, processed, 0, 0, $"Import dokončen – vytvořeno {processed} záznamů (placeholder).");
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
