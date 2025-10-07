using System.Linq;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace HWInventory.Api.Controllers;

[Route("api/labels")]
public class LabelsController : ApiControllerBase
{
    public LabelsController(IAppDbContext dbContext) : base(dbContext)
    {
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
    public async Task<IActionResult> RenderPdfAsync(Guid id)
    {
        var template = await DbContext.LabelTemplates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (template is null)
        {
            return NotFound();
        }

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(10);
                page.Content().Element(c => c
                    .Padding(10)
                    .Border(1)
                    .Column(col =>
                    {
                        col.Item().Text(template.Name).SemiBold().FontSize(18);
                        col.Item().Text($"Code: {template.Code}");
                        col.Item().Text("Sample payload preview");
                        col.Item().Text(template.Payload);
                    }));
            });
        });

        var stream = new MemoryStream();
        document.GeneratePdf(stream);
        stream.Position = 0;
        return File(stream, "application/pdf", $"{template.Code}.pdf");
    }

    [HttpPost("print-jobs")]
    public async Task<ActionResult<LabelPrintJob>> CreatePrintJobAsync([FromBody] CreatePrintJobRequest request)
    {
        var job = new LabelPrintJob
        {
            Target = request.Target,
            Format = request.Format,
            Status = "Queued",
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
}
