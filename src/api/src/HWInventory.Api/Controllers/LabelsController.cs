using System.Collections.Generic;
using System.Linq;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Api.Controllers;

[Route("api/labels")]
[Authorize(Policy = AuthorizationPolicies.LabelsManage)]
public class LabelsController : ApiControllerBase
{
    private readonly ILabelRenderingService _labelRenderingService;

    public LabelsController(IAppDbContext dbContext, ILabelRenderingService labelRenderingService) : base(dbContext)
    {
        _labelRenderingService = labelRenderingService;
    }

    [HttpGet("templates")]
    public async Task<ActionResult<IEnumerable<LabelTemplate>>> GetTemplatesAsync()
    {
        var templates = await DbContext.LabelTemplates.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        return Ok(templates);
    }

    [HttpPost("templates")]
    public async Task<ActionResult<LabelTemplate>> CreateTemplateAsync([FromBody] LabelTemplate request)
    {
        request.Id = Guid.NewGuid();
        request.CreatedAtUtc = DateTime.UtcNow;
        request.CreatedBy = User.Identity?.Name ?? "system";
        DbContext.LabelTemplates.Add(request);
        await DbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetTemplateByIdAsync), new { id = request.Id }, request);
    }

    [HttpGet("templates/{id:guid}")]
    public async Task<ActionResult<LabelTemplate>> GetTemplateByIdAsync(Guid id)
    {
        var template = await DbContext.LabelTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return template is null ? NotFound() : Ok(template);
    }

    [HttpPut("templates/{id:guid}")]
    public async Task<ActionResult<LabelTemplate>> UpdateTemplateAsync(Guid id, [FromBody] LabelTemplate request)
    {
        var template = await DbContext.LabelTemplates.FirstOrDefaultAsync(x => x.Id == id);
        if (template is null)
        {
            return NotFound();
        }

        template.Name = request.Name;
        template.Code = request.Code;
        template.Format = request.Format;
        template.Payload = request.Payload;
        template.Description = request.Description;
        template.ModifiedAtUtc = DateTime.UtcNow;
        template.ModifiedBy = User.Identity?.Name ?? "system";
        await DbContext.SaveChangesAsync();
        return Ok(template);
    }

    [HttpDelete("templates/{id:guid}")]
    public async Task<IActionResult> DeleteTemplateAsync(Guid id)
    {
        var template = await DbContext.LabelTemplates.FirstOrDefaultAsync(x => x.Id == id);
        if (template is null)
        {
            return NotFound();
        }

        DbContext.LabelTemplates.Remove(template);
        await DbContext.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("templates/{id:guid}/render/pdf")]
    public async Task<IActionResult> RenderPdfAsync(Guid id, [FromBody] LabelRenderRequest? request)
    {
        var template = await DbContext.LabelTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (template is null)
        {
            return NotFound();
        }

        var data = request?.Data ?? new Dictionary<string, string>();
        var pdf = _labelRenderingService.RenderPdf(template, data);
        return File(pdf, "application/pdf", $"{template.Code}.pdf");
    }

    [HttpPost("templates/{id:guid}/render/zpl")]
    public async Task<IActionResult> RenderZplAsync(Guid id, [FromBody] LabelRenderRequest? request)
    {
        var template = await DbContext.LabelTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (template is null)
        {
            return NotFound();
        }

        var data = request?.Data ?? new Dictionary<string, string>();
        var zpl = _labelRenderingService.RenderZpl(template, data);
        return Ok(new { zpl });
    }

    [HttpPost("print-jobs")]
    public async Task<ActionResult<LabelPrintJob>> CreatePrintJobAsync([FromBody] CreatePrintJobRequest request)
    {
        var job = new LabelPrintJob
        {
            Target = request.Target,
            Format = request.Format,
            JobStatus = "Queued",
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = User.Identity?.Name ?? "system",
            Items = request.Items.Select(item => new LabelPrintJobItem
            {
                AssetId = item.AssetId,
                AssetType = item.AssetType,
                Copies = item.Copies
            }).ToList()
        };

        DbContext.LabelPrintJobs.Add(job);
        await DbContext.SaveChangesAsync();
        return CreatedAtAction(nameof(GetPrintJobByIdAsync), new { id = job.Id }, job);
    }

    [HttpGet("print-jobs")]
    public async Task<ActionResult<IEnumerable<LabelPrintJob>>> GetPrintJobsAsync()
    {
        var jobs = await DbContext.LabelPrintJobs.AsNoTracking().Include(x => x.Items)
            .OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync();
        return Ok(jobs);
    }

    [HttpGet("print-jobs/{id:guid}")]
    public async Task<ActionResult<LabelPrintJob>> GetPrintJobByIdAsync(Guid id)
    {
        var job = await DbContext.LabelPrintJobs.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id);
        return job is null ? NotFound() : Ok(job);
    }

    public record CreatePrintJobRequest(string Target, string Format, IReadOnlyCollection<PrintJobItemDto> Items);

    public record PrintJobItemDto(Guid AssetId, string AssetType, int Copies);

    public record LabelRenderRequest(Dictionary<string, string>? Data);
}
