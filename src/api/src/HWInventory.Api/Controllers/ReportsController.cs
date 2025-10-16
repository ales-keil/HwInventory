using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HWInventory.Api.Models;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HWInventory.Api.Controllers;

[Authorize(Policy = AuthorizationPolicies.ReportsManage)]
[ApiController]
[Route("api/reports")]
public class ReportsController : ApiControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ReportDefinitionResponse>>> List([FromQuery] int page = 1, [FromQuery] int size = 50)
    {
        var models = await _reportService.ListAsync(page, size, HttpContext.RequestAborted);
        var responses = new List<ReportDefinitionResponse>();
        foreach (var model in models)
        {
            responses.Add(model.ToResponse());
        }

        return Ok(responses);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReportDefinitionResponse>> Get(Guid id)
    {
        var model = await _reportService.GetAsync(id, HttpContext.RequestAborted);
        if (model is null)
        {
            return NotFound();
        }

        return Ok(model.ToResponse());
    }

    [HttpPost]
    public async Task<ActionResult<ReportDefinitionResponse>> Create([FromBody] CreateReportRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var model = await _reportService.CreateAsync(request.ToCreateModel(), HttpContext.RequestAborted);
        return CreatedAtAction(nameof(Get), new { id = model.Id }, model.ToResponse());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ReportDefinitionResponse>> Update(Guid id, [FromBody] UpdateReportRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var model = await _reportService.UpdateAsync(id, request.ToUpdateModel(), HttpContext.RequestAborted);
        return Ok(model.ToResponse());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _reportService.DeleteAsync(id, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpPost("{id:guid}/run")]
    public async Task<ActionResult<ReportRunResponse>> Run(Guid id)
    {
        var run = await _reportService.TriggerAsync(id, HttpContext.RequestAborted);
        return Ok(run.ToResponse());
    }

    [HttpGet("{id:guid}/runs")]
    public async Task<ActionResult<IEnumerable<ReportRunResponse>>> ListRuns(Guid id, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var runs = await _reportService.ListRunsAsync(id, page, size, HttpContext.RequestAborted);
        var responses = new List<ReportRunResponse>();
        foreach (var run in runs)
        {
            responses.Add(run.ToResponse());
        }

        return Ok(responses);
    }

    [HttpGet("runs/{runId:guid}/download")]
    public async Task<IActionResult> Download(Guid runId)
    {
        var run = await _reportService.GetRunAsync(runId, HttpContext.RequestAborted);
        if (run is null)
        {
            return NotFound();
        }

        var artifact = await _reportService.GetArtifactAsync(runId, HttpContext.RequestAborted);
        if (artifact is null)
        {
            return NotFound();
        }

        var extension = Path.GetExtension(run.ArtifactPath ?? string.Empty);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".csv";
        }

        var fileName = $"report-{runId}{extension}";
        return File(artifact, "application/octet-stream", fileName);
    }
}
