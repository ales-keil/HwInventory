using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HWInventory.Infrastructure.Security;

public class EmailConnectorStore : IEmailConnectorStore
{
    private const string ConnectorType = "SMTP";

    private readonly IAppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<EmailConnectorStore> _logger;

    public EmailConnectorStore(IAppDbContext dbContext, IHttpContextAccessor httpContextAccessor, ILogger<EmailConnectorStore> logger)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<EmailConnectorModel?> GetAsync(CancellationToken cancellationToken = default)
    {
        var connector = await _dbContext.ConnectorProfiles
            .Include(x => x.Secrets)
            .FirstOrDefaultAsync(x => x.Type == ConnectorType, cancellationToken);

        if (connector is null)
        {
            return null;
        }

        var configuration = ParseConfiguration(connector.ConfigurationJson);

        return new EmailConnectorModel(
            connector.Id,
            connector.Alias,
            connector.Enabled,
            configuration.TryGetValue("host", out var host) ? host ?? string.Empty : string.Empty,
            configuration.TryGetValue("port", out var portValue) && int.TryParse(portValue, out var port) ? port : 25,
            configuration.TryGetValue("useTls", out var tlsValue) && bool.TryParse(tlsValue, out var useTls) && useTls,
            configuration.TryGetValue("username", out var username) ? username : null,
            connector.Secrets.Any(),
            configuration.TryGetValue("fromAddress", out var from) ? from : null,
            configuration.TryGetValue("replyToAddress", out var replyTo) ? replyTo : null,
            connector.HealthStatus,
            connector.LastTestedAtUtc);
    }

    public async Task<EmailConnectorModel> SaveAsync(EmailConnectorUpdate update, CancellationToken cancellationToken = default)
    {
        var actor = ResolveActor();
        var connector = await _dbContext.ConnectorProfiles
            .Include(x => x.Secrets)
            .FirstOrDefaultAsync(x => x.Type == ConnectorType, cancellationToken);

        var configuration = new Dictionary<string, string?>
        {
            ["host"] = update.Host,
            ["port"] = update.Port.ToString(),
            ["useTls"] = update.UseTls.ToString(),
            ["username"] = update.Username,
            ["fromAddress"] = update.FromAddress,
            ["replyToAddress"] = update.ReplyToAddress
        };

        if (connector is null)
        {
            connector = new ConnectorProfile
            {
                Type = ConnectorType,
                Alias = string.IsNullOrWhiteSpace(update.Alias) ? "Primary SMTP" : update.Alias,
                Enabled = update.Enabled,
                ConfigurationJson = JsonSerializer.Serialize(configuration),
                CreatedBy = actor,
                ModifiedBy = actor
            };

            _dbContext.ConnectorProfiles.Add(connector);
        }
        else
        {
            connector.Alias = string.IsNullOrWhiteSpace(update.Alias) ? connector.Alias : update.Alias;
            connector.Enabled = update.Enabled;
            connector.ConfigurationJson = JsonSerializer.Serialize(configuration);
            connector.ModifiedBy = actor;
        }

        if (update.RotateSecret && string.IsNullOrWhiteSpace(update.Password))
        {
            connector.Secrets.Clear();
        }
        else if (!string.IsNullOrWhiteSpace(update.Password))
        {
            connector.Secrets.Clear();
            connector.Secrets.Add(new ConnectorSecret
            {
                Key = "password",
                SecretReference = update.Password!,
                RotatedAtUtc = DateTime.UtcNow
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (await GetAsync(cancellationToken))!;
    }

    public async Task<EmailTestResult> SendTestAsync(EmailTestRequest request, CancellationToken cancellationToken = default)
    {
        var connector = await _dbContext.ConnectorProfiles
            .Include(x => x.Secrets)
            .FirstOrDefaultAsync(x => x.Type == ConnectorType && x.Enabled, cancellationToken);

        if (connector is null)
        {
            return new EmailTestResult(false, "SMTP connector is not configured or disabled.");
        }

        var configuration = ParseConfiguration(connector.ConfigurationJson);
        if (!configuration.TryGetValue("host", out var host) || string.IsNullOrWhiteSpace(host))
        {
            return new EmailTestResult(false, "SMTP host is missing.");
        }

        var fromAddress = configuration.TryGetValue("fromAddress", out var from) && !string.IsNullOrWhiteSpace(from)
            ? from!
            : "no-reply@example.com";

        var port = configuration.TryGetValue("port", out var portValue) && int.TryParse(portValue, out var parsedPort)
            ? parsedPort
            : 25;
        var useTls = configuration.TryGetValue("useTls", out var tlsValue) && bool.TryParse(tlsValue, out var tls) && tls;
        var username = configuration.TryGetValue("username", out var user) ? user : null;
        var password = connector.Secrets.FirstOrDefault()?.SecretReference;

        try
        {
#pragma warning disable SYSLIB0014
            using var smtp = new SmtpClient(host!, port)
            {
                EnableSsl = useTls
            };

            if (!string.IsNullOrWhiteSpace(username))
            {
                smtp.Credentials = new System.Net.NetworkCredential(username, password);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(fromAddress),
                Subject = request.Subject,
                Body = request.Body,
                IsBodyHtml = false
            };

            message.To.Add(new MailAddress(request.Recipient));

            if (configuration.TryGetValue("replyToAddress", out var replyTo) && !string.IsNullOrWhiteSpace(replyTo))
            {
                message.ReplyToList.Add(new MailAddress(replyTo));
            }

            if (request.Attachments is { Count: > 0 })
            {
                foreach (var attachment in request.Attachments)
                {
                    if (attachment?.Content is null || attachment.Content.Length == 0)
                    {
                        continue;
                    }

                    var stream = new MemoryStream(attachment.Content);
                    var mailAttachment = new Attachment(stream, attachment.FileName, attachment.ContentType);
                    message.Attachments.Add(mailAttachment);
                }
            }

            await smtp.SendMailAsync(message);
#pragma warning restore SYSLIB0014

            connector.HealthStatus = "Healthy";
            connector.LastTestedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new EmailTestResult(true, "Test e-mail was sent successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP test send failed");
            connector.HealthStatus = "Degraded";
            connector.LastTestedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new EmailTestResult(false, $"SMTP send failed: {ex.Message}");
        }
    }

    public async Task<EmailSendResult> SendNotificationAsync(EmailNotificationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Recipients.Count == 0)
        {
            return new EmailSendResult(false, "No recipients were provided for the notification email.");
        }

        var connector = await _dbContext.ConnectorProfiles
            .Include(x => x.Secrets)
            .FirstOrDefaultAsync(x => x.Type == ConnectorType && x.Enabled, cancellationToken);

        if (connector is null)
        {
            return new EmailSendResult(false, "SMTP connector is not configured or disabled.");
        }

        var configuration = ParseConfiguration(connector.ConfigurationJson);
        if (!configuration.TryGetValue("host", out var host) || string.IsNullOrWhiteSpace(host))
        {
            return new EmailSendResult(false, "SMTP host is missing.");
        }

        var fromAddress = configuration.TryGetValue("fromAddress", out var from) && !string.IsNullOrWhiteSpace(from)
            ? from!
            : null;

        if (string.IsNullOrWhiteSpace(fromAddress))
        {
            return new EmailSendResult(false, "From address must be configured before sending notifications.");
        }

        var port = configuration.TryGetValue("port", out var portValue) && int.TryParse(portValue, out var parsedPort)
            ? parsedPort
            : 25;
        var useTls = configuration.TryGetValue("useTls", out var tlsValue) && bool.TryParse(tlsValue, out var tls) && tls;
        var username = configuration.TryGetValue("username", out var user) ? user : null;
        var password = connector.Secrets.FirstOrDefault()?.SecretReference;

        try
        {
#pragma warning disable SYSLIB0014
            using var smtp = new SmtpClient(host!, port)
            {
                EnableSsl = useTls
            };

            if (!string.IsNullOrWhiteSpace(username))
            {
                smtp.Credentials = new System.Net.NetworkCredential(username, password);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(fromAddress),
                Subject = request.Subject,
                Body = request.Body,
                IsBodyHtml = request.IsBodyHtml
            };

            foreach (var recipient in request.Recipients)
            {
                if (!string.IsNullOrWhiteSpace(recipient))
                {
                    message.To.Add(new MailAddress(recipient));
                }
            }

            if (!message.To.Any())
            {
                return new EmailSendResult(false, "Notification email does not contain any valid recipients.");
            }

            if (configuration.TryGetValue("replyToAddress", out var replyTo) && !string.IsNullOrWhiteSpace(replyTo))
            {
                message.ReplyToList.Add(new MailAddress(replyTo));
            }

            if (request.Attachments is { Count: > 0 })
            {
                foreach (var attachment in request.Attachments)
                {
                    if (attachment?.Content is null || attachment.Content.Length == 0)
                    {
                        continue;
                    }

                    var stream = new MemoryStream(attachment.Content);
                    var mailAttachment = new Attachment(stream, attachment.FileName, attachment.ContentType);
                    message.Attachments.Add(mailAttachment);
                }
            }

            await smtp.SendMailAsync(message);
#pragma warning restore SYSLIB0014

            connector.HealthStatus = "Healthy";
            connector.LastTestedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new EmailSendResult(true, "Notification email sent successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP notification send failed");
            connector.HealthStatus = "Degraded";
            connector.LastTestedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new EmailSendResult(false, $"SMTP send failed: {ex.Message}");
        }
    }

    private static Dictionary<string, string?> ParseConfiguration(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? new Dictionary<string, string?>();
        }
        catch
        {
            return new Dictionary<string, string?>();
        }
    }

    private string ResolveActor()
    {
        return _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";
    }
}
