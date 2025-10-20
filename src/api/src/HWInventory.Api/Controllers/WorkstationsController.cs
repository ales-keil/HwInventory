using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HWInventory.Api.Models;
using HWInventory.Application.Abstractions;
using HWInventory.Application.Common;
using HWInventory.Domain.Entities;
using HWInventory.Domain.Enums;
using HWInventory.Domain.Security;
using HWInventory.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HWInventory.Api.Controllers;

[Route("api/workstations")]
public class WorkstationsController : ApiControllerBase
{
    private readonly IWorkstationHandoverService _handoverService;

    public WorkstationsController(IAppDbContext dbContext, IWorkstationHandoverService handoverService) : base(dbContext)
    {
        _handoverService = handoverService;
    }

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsRead)]
    public async Task<ActionResult<PagedResult<Workstation>>> GetAsync(
        [FromQuery] int page = 1,
        [FromQuery] int size = 50,
        [FromQuery] string? search = null,
        [FromQuery] EntityStatus? status = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] Guid? operatingSystemId = null,
        [FromQuery] Guid? workstationTypeId = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        size = Math.Clamp(size, 1, 200);

        var scope = await ResolveDataScopeAsync(cancellationToken);

        var query = DbContext.Workstations.AsNoTracking().AsQueryable();
        if (scope.HasLocationRestrictions)
        {
            var allowedLocations = scope.LocationIds.ToArray();
            query = query.Where(x => allowedLocations.Contains(x.LocationId));
        }

        if (scope.HasDepartmentRestrictions)
        {
            var allowedDepartments = scope.DepartmentKeys.ToArray();
            query = query.Where(x => x.OwnerDepartment != null && allowedDepartments.Contains(x.OwnerDepartment));
        }

        if (locationId.HasValue)
        {
            query = query.Where(x => x.LocationId == locationId.Value);
        }

        if (operatingSystemId.HasValue)
        {
            query = query.Where(x => x.OperatingSystemId == operatingSystemId.Value);
        }

        if (workstationTypeId.HasValue)
        {
            query = query.Where(x => x.WorkstationTypeId == workstationTypeId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.Like(x.Name, term) ||
                EF.Functions.Like(x.InventoryNumber, term) ||
                EF.Functions.Like(x.OwnerDisplayName ?? string.Empty, term) ||
                EF.Functions.Like(x.OwnerDepartment ?? string.Empty, term));
        }

        query = query.OrderBy(x => x.Name);

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);

        Response.Headers["X-Total-Count"] = total.ToString();
        var links = ServersController.GeneratePaginationLinksStatic("workstations", page, size, total);
        if (!string.IsNullOrEmpty(links))
        {
            Response.Headers["Link"] = links;
        }

        return Ok(new PagedResult<Workstation>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = size
        });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsRead)]
    public async Task<ActionResult<Workstation>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Workstations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(entity.LocationId))
        {
            return Forbid();
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(entity.OwnerDepartment) && !scope.DepartmentKeys.Contains(entity.OwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        return Ok(entity);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsManage)]
    public async Task<ActionResult<Workstation>> CreateAsync([FromBody] WorkstationRequestDto request, CancellationToken cancellationToken = default)
    {
        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(request.LocationId))
        {
            return Forbid();
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(request.OwnerDepartment) && !scope.DepartmentKeys.Contains(request.OwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var entity = Map(request);
        entity.CreatedAtUtc = DateTime.UtcNow;
        entity.CreatedBy = User.Identity?.Name ?? "system";

        DbContext.Workstations.Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetByIdAsync), new { id = entity.Id }, entity);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsManage)]
    public async Task<ActionResult<Workstation>> UpdateAsync(Guid id, [FromBody] WorkstationRequestDto request, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Workstations.Include(x => x.NetworkAssignments).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(entity.LocationId))
        {
            return Forbid();
        }

        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(request.LocationId))
        {
            return Forbid();
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(entity.OwnerDepartment) && !scope.DepartmentKeys.Contains(entity.OwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(request.OwnerDepartment) && !scope.DepartmentKeys.Contains(request.OwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        Update(entity, request);
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedBy = User.Identity?.Name ?? "system";
        await DbContext.SaveChangesAsync(cancellationToken);
        return Ok(entity);
    }

    [HttpPost("{id:guid}/handover")]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsManage)]
    public async Task<ActionResult> HandoverAsync(Guid id, [FromBody] HandoverRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Workstations.Include(x => x.NetworkAssignments).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(entity.LocationId))
        {
            return Forbid();
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(entity.OwnerDepartment) && !scope.DepartmentKeys.Contains(entity.OwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var forceRequested = request.Force;
        if (forceRequested && !User.IsInRole(SystemRoleNames.SuperAdmin))
        {
            return Forbid();
        }

        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(request.NewLocationId))
        {
            return Forbid();
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(request.NewOwnerDepartment) && !scope.DepartmentKeys.Contains(request.NewOwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (request.NewLocationId == Guid.Empty)
        {
            return BadRequest("New location is required.");
        }

        var oldOwnerId = entity.OwnerId;
        var oldOwnerDisplayName = entity.OwnerDisplayName;
        var oldOwnerDepartment = entity.OwnerDepartment;
        var oldLocationId = entity.LocationId;
        var oldLocationNote = entity.LocationNote;

        var dictionaryValues = await DbContext.DictionaryEntries
            .Where(x => x.DictType == "Location" && (x.Id == oldLocationId || x.Id == request.NewLocationId))
            .ToListAsync(cancellationToken);

        var oldLocationName = dictionaryValues.FirstOrDefault(x => x.Id == oldLocationId)?.Value;
        var newLocationName = dictionaryValues.FirstOrDefault(x => x.Id == request.NewLocationId)?.Value;

        var handoverTimestamp = DateTime.UtcNow;

        var toRecipients = NormalizeEmails(request.To);
        var ccRecipients = NormalizeEmails(request.Cc);
        var bccRecipients = NormalizeEmails(request.Bcc);

        if (forceRequested)
        {
            entity.OwnerId = request.NewOwnerId;
            entity.OwnerDisplayName = request.NewOwnerDisplayName;
            entity.OwnerDepartment = request.NewOwnerDepartment;
            entity.LocationId = request.NewLocationId;
            entity.LocationNote = request.NewLocationNote;
            if (request.NewAdminId.HasValue)
            {
                entity.PrimaryAdministratorId = request.NewAdminId;
            }

            entity.ModifiedAtUtc = handoverTimestamp;
            StampModification(entity);

            await DbContext.SaveChangesAsync(cancellationToken);

            var options = new WorkstationHandoverOptions(
                Actor: ResolveUserDisplayName(),
                OldOwnerDisplayName: oldOwnerDisplayName,
                OldOwnerDepartment: oldOwnerDepartment,
                OldLocationId: oldLocationId,
                OldLocationName: oldLocationName,
                OldLocationNote: oldLocationNote,
                NewOwnerDisplayName: request.NewOwnerDisplayName,
                NewOwnerDepartment: request.NewOwnerDepartment,
                NewLocationId: request.NewLocationId,
                NewLocationName: newLocationName,
                NewLocationNote: request.NewLocationNote,
                To: toRecipients,
                Cc: ccRecipients,
                Bcc: bccRecipients,
                Subject: request.Subject,
                MessageBody: request.MessageBody,
                Comment: request.Comment,
                HandoverAtUtc: handoverTimestamp,
                Force: true,
                AcceptUrl: null,
                DeclineUrl: null);

            var result = await _handoverService.ProcessAsync(entity, options, cancellationToken);

            AddAuditLog(
                nameof(Workstation),
                entity.Id,
                "HandoverForced",
                $"Předání dokončeno administrátorem na {request.NewOwnerDisplayName ?? "neuvedeno"} ({newLocationName ?? request.NewLocationId.ToString()}) – e-mail {(result.EmailSent ? "odeslán" : "neodeslán")}",
                new
                {
                    OldOwnerId = oldOwnerId,
                    NewOwnerId = entity.OwnerId,
                    OldOwnerDisplayName = oldOwnerDisplayName,
                    NewOwnerDisplayName = entity.OwnerDisplayName,
                    OldOwnerDepartment = oldOwnerDepartment,
                    NewOwnerDepartment = entity.OwnerDepartment,
                    OldLocationId = oldLocationId,
                    NewLocationId = entity.LocationId,
                    OldLocationName = oldLocationName,
                    NewLocationName = newLocationName,
                    To = toRecipients,
                    Cc = ccRecipients,
                    Bcc = bccRecipients,
                    Comment = request.Comment,
                    EmailMessage = result.Message
                });

            await DbContext.SaveChangesAsync(cancellationToken);

            return Ok(new { result.EmailSent, result.Message, PendingApproval = false, Forced = true });
        }

        if (toRecipients.Count == 0)
        {
            return BadRequest("Je nutné zadat alespoň jednoho příjemce e-mailu.");
        }

        var acceptToken = Guid.NewGuid().ToString("N");
        var declineToken = Guid.NewGuid().ToString("N");

        var handoverEntity = new WorkstationHandoverRequest
        {
            WorkstationId = entity.Id,
            OldOwnerId = oldOwnerId,
            OldOwnerDisplayName = oldOwnerDisplayName,
            OldOwnerDepartment = oldOwnerDepartment,
            OldLocationId = oldLocationId,
            OldLocationName = oldLocationName,
            OldLocationNote = oldLocationNote,
            NewOwnerId = request.NewOwnerId,
            NewOwnerDisplayName = request.NewOwnerDisplayName,
            NewOwnerDepartment = request.NewOwnerDepartment,
            NewLocationId = request.NewLocationId,
            NewLocationName = newLocationName,
            NewLocationNote = request.NewLocationNote,
            NewAdminId = request.NewAdminId,
            RequestedAtUtc = handoverTimestamp,
            RequestedBy = ResolveUserDisplayName(),
            Status = WorkstationHandoverStatus.Pending,
            TokenExpiresAtUtc = handoverTimestamp.AddDays(5),
            AcceptTokenHash = HashToken(acceptToken),
            DeclineTokenHash = HashToken(declineToken),
            Force = false,
            ToRecipientsJson = SerializeRecipients(toRecipients),
            CcRecipientsJson = SerializeRecipients(ccRecipients),
            BccRecipientsJson = SerializeRecipients(bccRecipients),
            Subject = request.Subject,
            MessageBody = request.MessageBody,
            Comment = request.Comment
        };

        DbContext.WorkstationHandovers.Add(handoverEntity);
        await DbContext.SaveChangesAsync(cancellationToken);

        var acceptUrl = BuildHandoverActionUrl(nameof(AcceptHandoverAsync), handoverEntity.Id, acceptToken);
        var declineUrl = BuildHandoverActionUrl(nameof(DeclineHandoverAsync), handoverEntity.Id, declineToken);

        var emailOptions = new WorkstationHandoverOptions(
            Actor: ResolveUserDisplayName(),
            OldOwnerDisplayName: oldOwnerDisplayName,
            OldOwnerDepartment: oldOwnerDepartment,
            OldLocationId: oldLocationId,
            OldLocationName: oldLocationName,
            OldLocationNote: oldLocationNote,
            NewOwnerDisplayName: request.NewOwnerDisplayName,
            NewOwnerDepartment: request.NewOwnerDepartment,
            NewLocationId: request.NewLocationId,
            NewLocationName: newLocationName,
            NewLocationNote: request.NewLocationNote,
            To: toRecipients,
            Cc: ccRecipients,
            Bcc: bccRecipients,
            Subject: request.Subject,
            MessageBody: request.MessageBody,
            Comment: request.Comment,
            HandoverAtUtc: handoverTimestamp,
            Force: false,
            AcceptUrl: acceptUrl,
            DeclineUrl: declineUrl);

        var emailResult = await _handoverService.ProcessAsync(entity, emailOptions, cancellationToken);
        emailResult = emailResult with { HandoverRequestId = handoverEntity.Id, PendingApproval = true };

        handoverEntity.EmailSent = emailResult.EmailSent;
        handoverEntity.EmailError = emailResult.EmailSent ? null : emailResult.Message;
        DbContext.WorkstationHandovers.Update(handoverEntity);

        AddAuditLog(
            nameof(Workstation),
            entity.Id,
            "HandoverRequested",
            $"Předání čeká na potvrzení nového vlastníka {request.NewOwnerDisplayName ?? "neuvedeno"} – e-mail {(emailResult.EmailSent ? "odeslán" : "neodeslán")}",
            new
            {
                OldOwnerId = oldOwnerId,
                ProposedOwnerId = request.NewOwnerId,
                OldOwnerDisplayName = oldOwnerDisplayName,
                ProposedOwnerDisplayName = request.NewOwnerDisplayName,
                OldOwnerDepartment = oldOwnerDepartment,
                ProposedOwnerDepartment = request.NewOwnerDepartment,
                OldLocationId = oldLocationId,
                ProposedLocationId = request.NewLocationId,
                OldLocationName = oldLocationName,
                ProposedLocationName = newLocationName,
                To = toRecipients,
                Cc = ccRecipients,
                Bcc = bccRecipients,
                Comment = request.Comment,
                HandoverId = handoverEntity.Id,
                EmailMessage = emailResult.Message
            });

        await DbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { emailResult.EmailSent, emailResult.Message, PendingApproval = true, HandoverId = handoverEntity.Id, AcceptUrl = acceptUrl, DeclineUrl = declineUrl });
    }

    [AllowAnonymous]
    [HttpGet("handover/{handoverId:guid}/accept")]
    public async Task<IActionResult> AcceptHandoverAsync(Guid handoverId, [FromQuery] string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BuildHandoverResponse("Token pro potvrzení je povinný.", false, 400);
        }

        var handover = await DbContext.WorkstationHandovers
            .Include(x => x.Workstation)
            .FirstOrDefaultAsync(x => x.Id == handoverId, cancellationToken);

        if (handover is null)
        {
            return BuildHandoverResponse("Předání nebylo nalezeno.", false, 404);
        }

        if (handover.Status != WorkstationHandoverStatus.Pending)
        {
            return BuildHandoverResponse("Toto předání již bylo zpracováno.", false);
        }

        if (handover.TokenExpiresAtUtc < DateTime.UtcNow)
        {
            handover.Status = WorkstationHandoverStatus.Cancelled;
            handover.CompletedAtUtc = DateTime.UtcNow;
            DbContext.WorkstationHandovers.Update(handover);
            await DbContext.SaveChangesAsync(cancellationToken);
            return BuildHandoverResponse("Platnost odkazu již vypršela. Kontaktujte prosím IT podporu.", false, 410);
        }

        if (!TokenMatches(handover.AcceptTokenHash, token))
        {
            return BuildHandoverResponse("Neplatný token pro potvrzení.", false, 401);
        }

        var workstation = handover.Workstation;
        var originalOwnerId = workstation.OwnerId;
        var originalOwnerDisplay = workstation.OwnerDisplayName;
        var originalLocationId = workstation.LocationId;
        var originalLocationName = handover.OldLocationName;

        workstation.OwnerId = handover.NewOwnerId;
        workstation.OwnerDisplayName = handover.NewOwnerDisplayName;
        workstation.OwnerDepartment = handover.NewOwnerDepartment;
        workstation.LocationId = handover.NewLocationId;
        workstation.LocationNote = handover.NewLocationNote;
        if (handover.NewAdminId.HasValue)
        {
            workstation.PrimaryAdministratorId = handover.NewAdminId;
        }

        workstation.ModifiedAtUtc = DateTime.UtcNow;
        workstation.ModifiedBy = handover.NewOwnerDisplayName ?? handover.RequestedBy;

        handover.Status = WorkstationHandoverStatus.Accepted;
        handover.CompletedAtUtc = DateTime.UtcNow;
        handover.CompletedBy = handover.NewOwnerDisplayName ?? "recipient";

        AddAuditLog(
            nameof(Workstation),
            workstation.Id,
            "HandoverAccepted",
            $"Předání potvrzeno novým vlastníkem {handover.NewOwnerDisplayName ?? "neuvedeno"}.",
            new
            {
                OldOwnerId = originalOwnerId,
                NewOwnerId = handover.NewOwnerId,
                OldOwnerDisplayName = originalOwnerDisplay,
                NewOwnerDisplayName = handover.NewOwnerDisplayName,
                OldLocationId = originalLocationId,
                NewLocationId = handover.NewLocationId,
                OldLocationName = originalLocationName,
                NewLocationName = handover.NewLocationName,
                Comment = handover.Comment,
                HandoverId = handover.Id
            });

        await DbContext.SaveChangesAsync(cancellationToken);

        return BuildHandoverResponse("Děkujeme, předání bylo úspěšně potvrzeno.", true);
    }

    [AllowAnonymous]
    [HttpGet("handover/{handoverId:guid}/decline")]
    public async Task<IActionResult> DeclineHandoverAsync(Guid handoverId, [FromQuery] string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BuildHandoverResponse("Token pro odmítnutí je povinný.", false, 400);
        }

        var handover = await DbContext.WorkstationHandovers
            .Include(x => x.Workstation)
            .FirstOrDefaultAsync(x => x.Id == handoverId, cancellationToken);

        if (handover is null)
        {
            return BuildHandoverResponse("Předání nebylo nalezeno.", false, 404);
        }

        if (handover.Status != WorkstationHandoverStatus.Pending)
        {
            return BuildHandoverResponse("Toto předání již bylo zpracováno.", false);
        }

        if (handover.TokenExpiresAtUtc < DateTime.UtcNow)
        {
            handover.Status = WorkstationHandoverStatus.Cancelled;
            handover.CompletedAtUtc = DateTime.UtcNow;
            DbContext.WorkstationHandovers.Update(handover);
            await DbContext.SaveChangesAsync(cancellationToken);
            return BuildHandoverResponse("Platnost odkazu již vypršela. Kontaktujte prosím IT podporu.", false, 410);
        }

        if (!TokenMatches(handover.DeclineTokenHash, token))
        {
            return BuildHandoverResponse("Neplatný token pro odmítnutí.", false, 401);
        }

        handover.Status = WorkstationHandoverStatus.Declined;
        handover.CompletedAtUtc = DateTime.UtcNow;
        handover.CompletedBy = handover.NewOwnerDisplayName ?? "recipient";

        AddAuditLog(
            nameof(Workstation),
            handover.WorkstationId,
            "HandoverDeclined",
            $"Předání bylo odmítnuto adresátem {handover.NewOwnerDisplayName ?? "neuvedeno"}.",
            new
            {
                ProposedOwnerId = handover.NewOwnerId,
                ProposedOwnerDisplayName = handover.NewOwnerDisplayName,
                ProposedLocationId = handover.NewLocationId,
                ProposedLocationName = handover.NewLocationName,
                Comment = handover.Comment,
                HandoverId = handover.Id
            });

        await DbContext.SaveChangesAsync(cancellationToken);

        return BuildHandoverResponse("Předání bylo odmítnuto. IT bude informováno.", true);
    }

    private IActionResult BuildHandoverResponse(string message, bool success, int statusCode = 200)
    {
        var html = $"<html><head><meta charset=\"utf-8\" /><title>HW Inventory – Handover</title></head><body style=\"font-family:Segoe UI,Arial,sans-serif;background-color:#0f172a0f;display:flex;align-items:center;justify-content:center;height:100vh;\"><div style=\"background:#ffffff;padding:32px;border-radius:12px;box-shadow:0 20px 40px rgba(15,23,42,0.15);max-width:480px;text-align:center;\"><h1 style=\"font-size:24px;color:#0f172a;margin-bottom:16px;\">{System.Net.WebUtility.HtmlEncode(success ? "Předání zařízení" : "Akce handover")}</h1><p style=\"font-size:16px;color:#1e293b;line-height:1.6;\">{System.Net.WebUtility.HtmlEncode(message)}</p></div></body></html>";
        return new ContentResult
        {
            StatusCode = statusCode,
            ContentType = "text/html; charset=utf-8",
            Content = html
        };
    }

    private static string SerializeRecipients(IReadOnlyCollection<string> recipients)
    {
        return JsonSerializer.Serialize(recipients, RecipientSerializerOptions);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private static bool TokenMatches(string storedHash, string providedToken)
    {
        try
        {
            var providedHash = HashToken(providedToken);
            var storedBytes = Convert.FromHexString(storedHash);
            var providedBytes = Convert.FromHexString(providedHash);
            return CryptographicOperations.FixedTimeEquals(storedBytes, providedBytes);
        }
        catch
        {
            return false;
        }
    }

    private string? BuildHandoverActionUrl(string actionName, Guid handoverId, string token)
    {
        var scheme = Request?.Scheme ?? "https";
        return Url.ActionLink(action: actionName, controller: null, values: new { handoverId, token }, protocol: scheme);
    }

    private static readonly JsonSerializerOptions RecipientSerializerOptions = new(JsonSerializerDefaults.Web);

    private static IReadOnlyCollection<string> NormalizeEmails(IReadOnlyCollection<string>? source)
    {
        if (source is null || source.Count == 0)
        {
            return Array.Empty<string>();
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var email in source)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            set.Add(email.Trim());
        }

        return set.ToArray();
    }

    [HttpPost("{id:guid}/retire")]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsManage)]
    public async Task<IActionResult> RetireAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Workstations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(entity.LocationId))
        {
            return Forbid();
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(entity.OwnerDepartment) && !scope.DepartmentKeys.Contains(entity.OwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        entity.Status = EntityStatus.Retired;
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedBy = User.Identity?.Name ?? "system";
        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("batch/retire")]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsManage)]
    public async Task<IActionResult> RetireBatchAsync([FromBody] BatchActionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Ids.Count == 0)
        {
            return BadRequest("No identifiers supplied.");
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        var entities = await DbContext.Workstations.Where(x => request.Ids.Contains(x.Id)).ToListAsync(cancellationToken);
        foreach (var entity in entities)
        {
            if (!IsWithinScope(scope, entity))
            {
                continue;
            }

            entity.Status = EntityStatus.Retired;
            entity.ModifiedAtUtc = DateTime.UtcNow;
            entity.ModifiedBy = User.Identity?.Name ?? "system";
        }

        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsManage)]
    public async Task<IActionResult> RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Workstations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(entity.LocationId))
        {
            return Forbid();
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(entity.OwnerDepartment) && !scope.DepartmentKeys.Contains(entity.OwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        entity.Status = EntityStatus.Active;
        entity.ModifiedAtUtc = DateTime.UtcNow;
        entity.ModifiedBy = User.Identity?.Name ?? "system";
        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("batch/restore")]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsManage)]
    public async Task<IActionResult> RestoreBatchAsync([FromBody] BatchActionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Ids.Count == 0)
        {
            return BadRequest("No identifiers supplied.");
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        var entities = await DbContext.Workstations.Where(x => request.Ids.Contains(x.Id)).ToListAsync(cancellationToken);
        foreach (var entity in entities)
        {
            if (!IsWithinScope(scope, entity))
            {
                continue;
            }

            entity.Status = EntityStatus.Active;
            entity.ModifiedAtUtc = DateTime.UtcNow;
            entity.ModifiedBy = User.Identity?.Name ?? "system";
        }

        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsManage)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Workstations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return NotFound();
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(entity.LocationId))
        {
            return Forbid();
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(entity.OwnerDepartment) && !scope.DepartmentKeys.Contains(entity.OwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        DbContext.Workstations.Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("batch/delete")]
    [Authorize(Policy = AuthorizationPolicies.WorkstationsManage)]
    public async Task<IActionResult> DeleteBatchAsync([FromBody] BatchActionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Ids.Count == 0)
        {
            return BadRequest("No identifiers supplied.");
        }

        var scope = await ResolveDataScopeAsync(cancellationToken);
        var entities = await DbContext.Workstations.Where(x => request.Ids.Contains(x.Id)).ToListAsync(cancellationToken);
        DbContext.Workstations.RemoveRange(entities.Where(entity => IsWithinScope(scope, entity)));

        await DbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static bool IsWithinScope(UserDataScope scope, Workstation entity)
    {
        if (scope.HasLocationRestrictions && !scope.LocationIds.Contains(entity.LocationId))
        {
            return false;
        }

        if (scope.HasDepartmentRestrictions && !string.IsNullOrEmpty(entity.OwnerDepartment) && !scope.DepartmentKeys.Contains(entity.OwnerDepartment, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static Workstation Map(WorkstationRequestDto request)
    {
        return new Workstation
        {
            Name = request.Name,
            InventoryNumber = request.InventoryNumber,
            OperatingSystemId = request.OperatingSystemId,
            WorkstationTypeId = request.WorkstationTypeId,
            OwnerId = request.OwnerId,
            OwnerDisplayName = request.OwnerDisplayName,
            OwnerDepartment = request.OwnerDepartment,
            LocationId = request.LocationId,
            LocationNote = request.LocationNote,
            Cpu = request.Cpu,
            Ram = request.Ram,
            Storage = request.Storage,
            MacAddress = request.MacAddress,
            PurchasedAt = request.PurchasedAt,
            SupportUntil = request.SupportUntil,
            PrimaryAdministratorId = request.PrimaryAdministratorId,
            SecondaryAdministratorId = request.SecondaryAdministratorId,
            Notes = request.Notes,
            NetworkAssignments = request.NetworkAssignments.Select(x => new NetworkEndpoint
            {
                Label = x.Label,
                VlanId = x.VlanId,
                IpAddress = x.IpAddress
            }).ToList()
        };
    }

    private static void Update(Workstation entity, WorkstationRequestDto request)
    {
        entity.Name = request.Name;
        entity.InventoryNumber = request.InventoryNumber;
        entity.OperatingSystemId = request.OperatingSystemId;
        entity.WorkstationTypeId = request.WorkstationTypeId;
        entity.OwnerId = request.OwnerId;
        entity.OwnerDisplayName = request.OwnerDisplayName;
        entity.OwnerDepartment = request.OwnerDepartment;
        entity.LocationId = request.LocationId;
        entity.LocationNote = request.LocationNote;
        entity.Cpu = request.Cpu;
        entity.Ram = request.Ram;
        entity.Storage = request.Storage;
        entity.MacAddress = request.MacAddress;
        entity.PurchasedAt = request.PurchasedAt;
        entity.SupportUntil = request.SupportUntil;
        entity.PrimaryAdministratorId = request.PrimaryAdministratorId;
        entity.SecondaryAdministratorId = request.SecondaryAdministratorId;
        entity.Notes = request.Notes;

        entity.NetworkAssignments.Clear();
        foreach (var assignment in request.NetworkAssignments)
        {
            entity.NetworkAssignments.Add(new NetworkEndpoint
            {
                Label = assignment.Label,
                VlanId = assignment.VlanId,
                IpAddress = assignment.IpAddress
            });
        }
    }

    public record HandoverRequest(
        Guid? NewOwnerId,
        string? NewOwnerDisplayName,
        string? NewOwnerDepartment,
        Guid NewLocationId,
        string? NewLocationNote,
        Guid? NewAdminId,
        IReadOnlyCollection<string>? To,
        IReadOnlyCollection<string>? Cc,
        IReadOnlyCollection<string>? Bcc,
        string? Subject,
        string? MessageBody,
        string? Comment,
        bool Force);
}
