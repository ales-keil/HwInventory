using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Infrastructure.Maintenance;

public class BackupConfigurationStore : IBackupConfigurationStore
{
    private readonly IAppDbContext _dbContext;
    private readonly ISecretProtector _secretProtector;

    public BackupConfigurationStore(IAppDbContext dbContext, ISecretProtector secretProtector)
    {
        _dbContext = dbContext;
        _secretProtector = secretProtector;
    }

    public async Task<BackupScheduleModel> GetAsync(CancellationToken cancellationToken = default)
    {
        var schedule = await _dbContext.BackupSchedules.FirstOrDefaultAsync(cancellationToken);
        if (schedule is null)
        {
            schedule = new BackupSchedule
            {
                Enabled = false,
                Frequency = "Daily",
                ExecutionTimeUtc = TimeSpan.FromHours(1),
                Scope = "Full",
                StoragePath = Path.Combine(AppContext.BaseDirectory, "backups"),
                IntegrityCheckEnabled = true,
                SendEmail = false
            };

            _dbContext.BackupSchedules.Add(schedule);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Map(schedule);
    }

    public async Task<BackupScheduleModel> SaveAsync(BackupScheduleUpdate update, CancellationToken cancellationToken = default)
    {
        var schedule = await _dbContext.BackupSchedules.FirstOrDefaultAsync(cancellationToken);
        if (schedule is null)
        {
            schedule = new BackupSchedule();
            _dbContext.BackupSchedules.Add(schedule);
        }

        schedule.Enabled = update.Enabled;
        schedule.Frequency = update.Frequency;
        schedule.DayOfWeek = update.DayOfWeek;
        schedule.DayOfMonth = update.DayOfMonth;
        schedule.ExecutionTimeUtc = update.ExecutionTimeUtc;
        schedule.Scope = update.Scope;
        schedule.StoragePath = update.StoragePath;
        schedule.EncryptionEnabled = update.EncryptionEnabled;
        schedule.SendEmail = update.SendEmail;
        schedule.EmailRecipients = string.IsNullOrWhiteSpace(update.EmailRecipients) ? null : update.EmailRecipients;
        schedule.IntegrityCheckEnabled = update.IntegrityCheckEnabled;

        if (update.RotatePassword)
        {
            schedule.ProtectedEncryptionSecret = null;
        }

        if (update.EncryptionEnabled && !string.IsNullOrWhiteSpace(update.Password))
        {
            schedule.ProtectedEncryptionSecret = _secretProtector.Protect(update.Password);
        }

        schedule.NextRunAtUtc = CalculateNextRunUtc(schedule, DateTime.UtcNow);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(schedule);
    }

    public async Task<BackupScheduleTriggerResult> TryMarkTriggeredAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var schedule = await _dbContext.BackupSchedules.FirstOrDefaultAsync(cancellationToken);
        if (schedule is null || !schedule.Enabled)
        {
            return new BackupScheduleTriggerResult(false, schedule is null ? null : Map(schedule), null);
        }

        if (!schedule.NextRunAtUtc.HasValue)
        {
            schedule.NextRunAtUtc = CalculateNextRunUtc(schedule, utcNow);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new BackupScheduleTriggerResult(false, Map(schedule), schedule.ProtectedEncryptionSecret);
        }

        if (schedule.NextRunAtUtc > utcNow)
        {
            return new BackupScheduleTriggerResult(false, Map(schedule), schedule.ProtectedEncryptionSecret);
        }

        schedule.LastRunAtUtc = utcNow;
        schedule.NextRunAtUtc = CalculateNextRunUtc(schedule, utcNow.AddMinutes(1));
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new BackupScheduleTriggerResult(true, Map(schedule), schedule.ProtectedEncryptionSecret);
    }

    private static BackupScheduleModel Map(BackupSchedule schedule)
    {
        return new BackupScheduleModel(
            schedule.Enabled,
            schedule.Frequency,
            schedule.DayOfWeek,
            schedule.DayOfMonth,
            schedule.ExecutionTimeUtc,
            schedule.Scope,
            schedule.StoragePath,
            schedule.EncryptionEnabled,
            !string.IsNullOrEmpty(schedule.ProtectedEncryptionSecret),
            schedule.SendEmail,
            schedule.EmailRecipients,
            schedule.IntegrityCheckEnabled,
            schedule.LastRunAtUtc,
            schedule.NextRunAtUtc);
    }

    private static DateTime? CalculateNextRunUtc(BackupSchedule schedule, DateTime referenceUtc)
    {
        if (!schedule.Enabled)
        {
            return null;
        }

        var executionDate = new DateTime(referenceUtc.Year, referenceUtc.Month, referenceUtc.Day, 0, 0, 0, DateTimeKind.Utc)
            .Add(schedule.ExecutionTimeUtc);

        if (schedule.Frequency.Equals("Daily", StringComparison.OrdinalIgnoreCase))
        {
            if (executionDate <= referenceUtc)
            {
                executionDate = executionDate.AddDays(1);
            }

            return executionDate;
        }

        if (schedule.Frequency.Equals("Weekly", StringComparison.OrdinalIgnoreCase))
        {
            var dayOfWeek = schedule.DayOfWeek ?? 0;
            var currentDay = (int)executionDate.DayOfWeek;
            var daysToAdd = ((dayOfWeek - currentDay) + 7) % 7;
            if (daysToAdd == 0 && executionDate <= referenceUtc)
            {
                daysToAdd = 7;
            }

            return executionDate.AddDays(daysToAdd);
        }

        if (schedule.Frequency.Equals("Monthly", StringComparison.OrdinalIgnoreCase))
        {
            var day = schedule.DayOfMonth ?? 1;
            var daysInMonth = DateTime.DaysInMonth(executionDate.Year, executionDate.Month);
            day = Math.Min(Math.Max(day, 1), daysInMonth);
            executionDate = new DateTime(executionDate.Year, executionDate.Month, day, executionDate.Hour, executionDate.Minute, executionDate.Second, DateTimeKind.Utc);
            if (executionDate <= referenceUtc)
            {
                var nextMonth = executionDate.AddMonths(1);
                daysInMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                day = Math.Min(Math.Max(schedule.DayOfMonth ?? day, 1), daysInMonth);
                executionDate = new DateTime(nextMonth.Year, nextMonth.Month, day, nextMonth.Hour, nextMonth.Minute, nextMonth.Second, DateTimeKind.Utc);
            }

            return executionDate;
        }

        return executionDate <= referenceUtc ? executionDate.AddDays(1) : executionDate;
    }
}
