using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Api.Models;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Enums;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace HWInventory.Api.Controllers;

[Authorize(Policy = AuthorizationPolicies.MaintenanceManage)]
[Route("api/maintenance")]
public class MaintenanceController : ApiControllerBase
{
    private readonly IBackupService _backupService;
    private readonly IBackupConfigurationStore _scheduleStore;

    public MaintenanceController(IAppDbContext dbContext, IBackupService backupService, IBackupConfigurationStore scheduleStore)
        : base(dbContext)
    {
        _backupService = backupService;
        _scheduleStore = scheduleStore;
    }

    [HttpGet("backups/history")]
    public async Task<ActionResult> GetHistoryAsync([FromQuery] int page = 1, [FromQuery] int size = 20, CancellationToken cancellationToken = default)
    {
        size = Math.Clamp(size, 1, 100);
        var history = await _backupService.ListHistoryAsync(page, size, cancellationToken);
        return Ok(history.Select(Map));
    }

    [HttpPost("backups")]
    public async Task<ActionResult<BackupResponseDto>> QueueBackupAsync([FromBody] BackupRequestDto request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<BackupScope>(request.Scope, true, out var scope))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Neplatný rozsah",
                Detail = "Zvolený rozsah zálohy není podporován.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (string.IsNullOrWhiteSpace(request.StoragePath))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Chybí cesta",
                Detail = "Je nutné zadat cílovou cestu pro uložení zálohy.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (request.EncryptionEnabled && request.Password != request.PasswordConfirmation)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Potvrzení hesla nesouhlasí",
                Detail = "Zadané heslo a potvrzení se liší.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var job = await _backupService.QueueBackupAsync(new BackupRequest(
            scope,
            request.StoragePath,
            request.EncryptionEnabled,
            request.EncryptionEnabled ? request.Password : null,
            null,
            request.SendEmail,
            request.EmailRecipients,
            request.IntegrityCheckEnabled,
            false),
            cancellationToken);

        AddAuditLog("Maintenance.Backup", job.Id, "Create", "Založena nová záloha", job);
        await DbContext.SaveChangesAsync(cancellationToken);

        return AcceptedAtAction(nameof(GetBackupAsync), new { id = job.Id }, Map(job));
    }

    [HttpGet("backups/{id:guid}")]
    public async Task<ActionResult<BackupResponseDto>> GetBackupAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await _backupService.GetAsync(id, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        return Ok(Map(job));
    }

    [HttpPost("backups/{id:guid}/restore")]
    public async Task<ActionResult> RestoreAsync(Guid id, [FromBody] RestoreRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _backupService.RestoreAsync(id, new RestoreRequest(request.Password, request.PerformIntegrityTest), cancellationToken);
        AddAuditLog("Maintenance.Backup", id, "Restore", "Obnova zálohy", result);
        await DbContext.SaveChangesAsync(cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Obnova selhala",
                Detail = result.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new { message = result.Message });
    }

    [HttpPost("backups/{id:guid}/test")]
    public async Task<ActionResult> TestIntegrityAsync(Guid id, [FromBody] IntegrityTestRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _backupService.TestIntegrityAsync(id, request.Password, cancellationToken);
        AddAuditLog("Maintenance.Backup", id, "IntegrityTest", "Ověření integrity zálohy", result);
        await DbContext.SaveChangesAsync(cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Test integrity selhal",
                Detail = result.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }

        return Ok(new { message = result.Message, passed = result.Passed });
    }

    [HttpGet("schedule")]
    public async Task<ActionResult<BackupScheduleResponseDto>> GetScheduleAsync(CancellationToken cancellationToken)
    {
        var schedule = await _scheduleStore.GetAsync(cancellationToken);
        return Ok(Map(schedule));
    }

    [HttpPut("schedule")]
    public async Task<ActionResult<BackupScheduleResponseDto>> UpdateScheduleAsync([FromBody] BackupScheduleRequestDto request, CancellationToken cancellationToken)
    {
        if (request.EncryptionEnabled && request.Password != request.PasswordConfirmation && !request.RotatePassword)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Potvrzení hesla nesouhlasí",
                Detail = "Zadané heslo a potvrzení se liší.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (string.IsNullOrWhiteSpace(request.StoragePath))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Chybí cesta",
                Detail = "Je nutné zadat cílovou cestu pro zálohy.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (!TimeSpan.TryParse(request.ExecutionTimeUtc, out var executionTime))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Neplatný čas",
                Detail = "Čas spuštění musí být ve formátu HH:mm.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var schedule = await _scheduleStore.SaveAsync(new BackupScheduleUpdate(
            request.Enabled,
            request.Frequency,
            request.DayOfWeek,
            request.DayOfMonth,
            executionTime,
            request.Scope,
            request.StoragePath,
            request.EncryptionEnabled,
            request.RotatePassword ? request.Password : request.EncryptionEnabled ? request.Password : null,
            request.RotatePassword,
            request.SendEmail,
            request.EmailRecipients,
            request.IntegrityCheckEnabled),
            cancellationToken);

        AddAuditLog("Maintenance.BackupSchedule", Guid.Empty, "Update", "Aktualizace plánu záloh", schedule);
        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(Map(schedule));
    }

    private static BackupResponseDto Map(BackupJobModel model)
    {
        return new BackupResponseDto
        {
            Id = model.Id,
            Scope = model.Scope,
            FileName = model.FileName,
            StoragePath = model.StoragePath,
            EncryptionEnabled = model.EncryptionEnabled,
            SendEmail = model.SendEmail,
            IntegrityCheckEnabled = model.IntegrityCheckEnabled,
            IsAutomatic = model.IsAutomatic,
            Status = model.Status,
            FileSizeBytes = model.FileSizeBytes,
            CompletedAtUtc = model.CompletedAtUtc,
            IntegrityPassed = model.IntegrityPassed,
            CreatedAtUtc = model.CreatedAtUtc,
            CreatedBy = model.CreatedBy,
            FailureReason = model.FailureReason
        };
    }

    private static BackupScheduleResponseDto Map(BackupScheduleModel model)
    {
        return new BackupScheduleResponseDto
        {
            Enabled = model.Enabled,
            Frequency = model.Frequency,
            DayOfWeek = model.DayOfWeek,
            DayOfMonth = model.DayOfMonth,
            ExecutionTimeUtc = model.ExecutionTimeUtc.ToString("hh\:mm"),
            Scope = model.Scope,
            StoragePath = model.StoragePath,
            EncryptionEnabled = model.EncryptionEnabled,
            HasStoredPassword = model.HasStoredPassword,
            SendEmail = model.SendEmail,
            EmailRecipients = model.EmailRecipients,
            IntegrityCheckEnabled = model.IntegrityCheckEnabled,
            LastRunAtUtc = model.LastRunAtUtc,
            NextRunAtUtc = model.NextRunAtUtc
        };
    }
}
