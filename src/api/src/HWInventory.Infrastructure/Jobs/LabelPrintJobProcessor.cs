using System;
using System.Linq;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace HWInventory.Infrastructure.Jobs;

public class LabelPrintJobProcessor : IJob
{
    private readonly IAppDbContext _dbContext;
    private readonly ILogger<LabelPrintJobProcessor> _logger;

    public LabelPrintJobProcessor(IAppDbContext dbContext, ILogger<LabelPrintJobProcessor> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var queuedJobs = await _dbContext.LabelPrintJobs
            .Where(job => job.JobStatus == "Queued")
            .Include(job => job.Items)
            .OrderBy(job => job.CreatedAtUtc)
            .Take(10)
            .ToListAsync(context.CancellationToken);

        if (!queuedJobs.Any())
        {
            return;
        }

        foreach (var job in queuedJobs)
        {
            job.JobStatus = "Processed";
            job.ModifiedAtUtc = DateTime.UtcNow;
            job.ModifiedBy = "system-job";
            _logger.LogInformation("Processed label print job {JobId} targeting {Target}", job.Id, job.Target);
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
