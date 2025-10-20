using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Api.Models;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HWInventory.Api.Controllers;

[Route("api/settings/handover")]
[Authorize(Policy = AuthorizationPolicies.SettingsManage)]
public class HandoverController : ApiControllerBase
{
    private readonly IHandoverConfigurationStore _store;

    public HandoverController(IAppDbContext dbContext, IHandoverConfigurationStore store) : base(dbContext)
    {
        _store = store;
    }

    [HttpGet]
    public async Task<ActionResult<HandoverConfigurationResponse>> GetAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await _store.GetAsync(cancellationToken);
        return Ok(Map(configuration));
    }

    [HttpPut]
    public async Task<ActionResult<HandoverConfigurationResponse>> UpdateAsync(
        [FromBody] HandoverConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        if ((request.DefaultTo?.Count ?? 0) > 3 || (request.DefaultCc?.Count ?? 0) > 3 || (request.DefaultBcc?.Count ?? 0) > 3)
        {
            return BadRequest("Každá sekce příjemců podporuje maximálně tři adresy.");
        }

        var before = await _store.GetAsync(cancellationToken);

        var update = new HandoverConfigurationModel(
            NormalizeList(request.DefaultTo),
            NormalizeList(request.DefaultCc),
            NormalizeList(request.DefaultBcc),
            NormalizeString(request.DefaultSubject),
            NormalizeString(request.DefaultBody),
            NormalizeString(request.PdfLogoBase64),
            request.UseMinimalPdf,
            NormalizeString(request.PdfFooterNote));

        var after = await _store.SaveAsync(update, cancellationToken);

        AddAuditLog(
            "Settings.Handover",
            Guid.Empty,
            "Update",
            "Aktualizace nastavení předávacího protokolu",
            new { Before = before, After = after });

        return Ok(Map(after));
    }

    private static HandoverConfigurationResponse Map(HandoverConfigurationModel model)
    {
        return new HandoverConfigurationResponse(
            model.DefaultTo,
            model.DefaultCc,
            model.DefaultBcc,
            model.DefaultSubject,
            model.DefaultBody,
            model.PdfLogoBase64,
            model.UseMinimalPdf,
            model.PdfFooterNote);
    }

    private static IReadOnlyList<string> NormalizeList(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? NormalizeString(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
