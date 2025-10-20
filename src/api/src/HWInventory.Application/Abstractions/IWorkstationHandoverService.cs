using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Domain.Entities;

namespace HWInventory.Application.Abstractions;

public interface IWorkstationHandoverService
{
    Task<WorkstationHandoverResult> ProcessAsync(
        Workstation workstation,
        WorkstationHandoverOptions options,
        CancellationToken cancellationToken = default);
}

public record WorkstationHandoverOptions(
    string? Actor,
    string? OldOwnerDisplayName,
    string? OldOwnerDepartment,
    Guid OldLocationId,
    string? OldLocationName,
    string? OldLocationNote,
    string? NewOwnerDisplayName,
    string? NewOwnerDepartment,
    Guid NewLocationId,
    string? NewLocationName,
    string? NewLocationNote,
    IReadOnlyCollection<string> To,
    IReadOnlyCollection<string>? Cc,
    IReadOnlyCollection<string>? Bcc,
    string? Subject,
    string? MessageBody,
    string? Comment,
    DateTime HandoverAtUtc,
    bool Force,
    string? AcceptUrl,
    string? DeclineUrl);

public record WorkstationHandoverResult(bool EmailSent, string? Message, byte[] PdfBytes, Guid? HandoverRequestId, bool PendingApproval);
