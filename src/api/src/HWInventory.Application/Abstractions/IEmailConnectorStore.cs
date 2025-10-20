using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HWInventory.Application.Abstractions;

public interface IEmailConnectorStore
{
    Task<EmailConnectorModel?> GetAsync(CancellationToken cancellationToken = default);
    Task<EmailConnectorModel> SaveAsync(EmailConnectorUpdate update, CancellationToken cancellationToken = default);
    Task<EmailTestResult> SendTestAsync(EmailTestRequest request, CancellationToken cancellationToken = default);
    Task<EmailSendResult> SendNotificationAsync(EmailNotificationRequest request, CancellationToken cancellationToken = default);
}

public record EmailConnectorModel(
    Guid Id,
    string Alias,
    bool Enabled,
    string Host,
    int Port,
    bool UseTls,
    string? Username,
    bool HasPassword,
    string? FromAddress,
    string? ReplyToAddress,
    string? HealthStatus,
    DateTime? LastTestedAtUtc);

public record EmailConnectorUpdate(
    string Alias,
    bool Enabled,
    string Host,
    int Port,
    bool UseTls,
    string? Username,
    string? FromAddress,
    string? ReplyToAddress,
    bool RotateSecret,
    string? Password);

public record EmailTestRequest(string Recipient, string Subject, string Body);

public record EmailTestResult(bool Success, string Message);

public record EmailNotificationRequest(
    IReadOnlyCollection<string> Recipients,
    string Subject,
    string Body,
    bool IsBodyHtml,
    IReadOnlyCollection<EmailAttachment>? Attachments = null);

public record EmailSendResult(bool Success, string Message);

public record EmailAttachment(string FileName, byte[] Content, string ContentType);
