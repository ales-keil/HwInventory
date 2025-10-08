using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using HWInventory.Api.Models;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HWInventory.Api.Controllers;

[Authorize(Policy = AuthorizationPolicies.ImportExportManage)]
[ApiController]
[Route("api/imports")]
public class ImportsController : ApiControllerBase
{
    private readonly IImportService _importService;

    public ImportsController(IImportService importService)
    {
        _importService = importService;
    }

    [HttpPost]
    public async Task<ActionResult<ImportJobResponse>> QueueImport([FromBody] QueueImportRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var model = await _importService.QueueImportAsync(
            new ImportRequest(
                request.Scope,
                request.Format,
                request.ConflictStrategy,
                request.DryRun,
                request.StoragePath,
                request.FileName,
                request.ContentBase64,
                request.SendEmail,
                request.EmailRecipients,
                request.MappingJson),
            HttpContext.RequestAborted);

        return CreatedAtAction(nameof(GetJob), new { id = model.Id }, ImportJobResponse.FromModel(model));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ImportJobResponse>>> List([FromQuery] int page = 1, [FromQuery] int size = 50)
    {
        var models = await _importService.ListHistoryAsync(page, size, HttpContext.RequestAborted);
        var response = new List<ImportJobResponse>();
        foreach (var model in models)
        {
            response.Add(ImportJobResponse.FromModel(model));
        }

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ImportJobResponse>> GetJob(Guid id)
    {
        var model = await _importService.GetAsync(id, HttpContext.RequestAborted);
        if (model is null)
        {
            return NotFound();
        }

        return Ok(ImportJobResponse.FromModel(model));
    }

    [HttpGet("{id:guid}/log")]
    public async Task<IActionResult> DownloadLog(Guid id)
    {
        var model = await _importService.GetAsync(id, HttpContext.RequestAborted);
        if (model is null || string.IsNullOrWhiteSpace(model.ResultLog))
        {
            return NotFound();
        }

        var bytes = Encoding.UTF8.GetBytes(model.ResultLog);
        return File(bytes, "text/plain", $"import-{id}.log");
    }
}
