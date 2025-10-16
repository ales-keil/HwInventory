using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HWInventory.Infrastructure.Workstations;

public class WorkstationHandoverService : IWorkstationHandoverService
{
    private readonly IAppDbContext _dbContext;
    private readonly ILogger<WorkstationHandoverService> _logger;

    private const string ConnectorType = "SMTP";

    public WorkstationHandoverService(IAppDbContext dbContext, ILogger<WorkstationHandoverService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<WorkstationHandoverResult> ProcessAsync(
        Workstation workstation,
        WorkstationHandoverOptions options,
        CancellationToken cancellationToken = default)
    {
        var pdfBytes = GeneratePdf(workstation, options);

        var recipients = options.To?.Where(NotNullOrWhiteSpace).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
        var ccRecipients = options.Cc?.Where(NotNullOrWhiteSpace).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
        var bccRecipients = options.Bcc?.Where(NotNullOrWhiteSpace).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();

        if (recipients.Length == 0)
        {
            _logger.LogWarning("Workstation handover triggered without recipients (WorkstationId: {WorkstationId}).", workstation.Id);
            return new WorkstationHandoverResult(false, "No e-mail recipients were provided.", pdfBytes);
        }

        var connector = await _dbContext.ConnectorProfiles
            .Include(x => x.Secrets)
            .FirstOrDefaultAsync(x => x.Type == ConnectorType && x.Enabled, cancellationToken);

        if (connector is null)
        {
            _logger.LogWarning("SMTP connector is not configured – skipping handover e-mail for workstation {WorkstationId}", workstation.Id);
            return new WorkstationHandoverResult(false, "SMTP connector is not configured.", pdfBytes);
        }

        var configuration = ParseConfiguration(connector.ConfigurationJson);
        if (!configuration.TryGetValue("host", out var host) || string.IsNullOrWhiteSpace(host))
        {
            _logger.LogWarning("SMTP connector is missing host – skipping handover e-mail for workstation {WorkstationId}", workstation.Id);
            return new WorkstationHandoverResult(false, "SMTP host is not configured.", pdfBytes);
        }

        var port = configuration.TryGetValue("port", out var portValue) && int.TryParse(portValue, out var parsedPort) ? parsedPort : 25;
        var useTls = configuration.TryGetValue("useTls", out var tlsValue) && bool.TryParse(tlsValue, out var tlsEnabled) && tlsEnabled;
        var username = configuration.TryGetValue("username", out var userValue) ? userValue : null;
        var password = connector.Secrets.FirstOrDefault(x => x.Key == "password")?.SecretReference;
        var fromAddress = configuration.TryGetValue("fromAddress", out var fromValue) && !string.IsNullOrWhiteSpace(fromValue)
            ? fromValue!
            : "no-reply@localhost";
        var replyTo = configuration.TryGetValue("replyToAddress", out var replyToValue) ? replyToValue : null;

        try
        {
#pragma warning disable SYSLIB0014
            using var smtp = new SmtpClient(host!, port)
            {
                EnableSsl = useTls
            };
#pragma warning restore SYSLIB0014

            if (!string.IsNullOrWhiteSpace(username))
            {
                smtp.Credentials = new System.Net.NetworkCredential(username, password);
            }

            using var message = BuildMailMessage(workstation, options, fromAddress, replyTo, pdfBytes);

            foreach (var recipient in recipients)
            {
                message.To.Add(new MailAddress(recipient));
            }

            foreach (var recipient in ccRecipients)
            {
                message.CC.Add(new MailAddress(recipient));
            }

            foreach (var recipient in bccRecipients)
            {
                message.Bcc.Add(new MailAddress(recipient));
            }

            await smtp.SendMailAsync(message);
            return new WorkstationHandoverResult(true, "Handover e-mail sent successfully.", pdfBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send handover e-mail for workstation {WorkstationId}", workstation.Id);
            return new WorkstationHandoverResult(false, $"Failed to send e-mail: {ex.Message}", pdfBytes);
        }
    }

    private static MailMessage BuildMailMessage(Workstation workstation, WorkstationHandoverOptions options, string fromAddress, string? replyTo, byte[] pdfBytes)
    {
        var subject = string.IsNullOrWhiteSpace(options.Subject)
            ? $"Předávací protokol – {workstation.Name} ({workstation.InventoryNumber})"
            : options.Subject!;

        var bodyBuilder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(options.MessageBody))
        {
            bodyBuilder.AppendLine(options.MessageBody!.Trim());
            bodyBuilder.AppendLine();
        }

        bodyBuilder.AppendLine($"Zařízení: {workstation.Name} ({workstation.InventoryNumber})");
        bodyBuilder.AppendLine($"Předáno dne: {options.HandoverAtUtc:dd.MM.yyyy HH:mm} UTC");
        bodyBuilder.AppendLine($"Předal: {options.Actor ?? "N/A"}");
        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("Původní stav:");
        bodyBuilder.AppendLine($"  Uživatel: {options.OldOwnerDisplayName ?? "Neuvedeno"}");
        bodyBuilder.AppendLine($"  Oddělení: {options.OldOwnerDepartment ?? "Neuvedeno"}");
        bodyBuilder.AppendLine($"  Umístění: {options.OldLocationName ?? options.OldLocationId.ToString()}");
        if (!string.IsNullOrWhiteSpace(options.OldLocationNote))
        {
            bodyBuilder.AppendLine($"  Poznámka: {options.OldLocationNote}");
        }

        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("Nový stav:");
        bodyBuilder.AppendLine($"  Uživatel: {options.NewOwnerDisplayName ?? "Neuvedeno"}");
        bodyBuilder.AppendLine($"  Oddělení: {options.NewOwnerDepartment ?? "Neuvedeno"}");
        bodyBuilder.AppendLine($"  Umístění: {options.NewLocationName ?? options.NewLocationId.ToString()}");
        if (!string.IsNullOrWhiteSpace(options.NewLocationNote))
        {
            bodyBuilder.AppendLine($"  Poznámka: {options.NewLocationNote}");
        }

        if (!string.IsNullOrWhiteSpace(options.Comment))
        {
            bodyBuilder.AppendLine();
            bodyBuilder.AppendLine("Komentář:");
            bodyBuilder.AppendLine(options.Comment);
        }

        var message = new MailMessage
        {
            From = new MailAddress(fromAddress),
            Subject = subject,
            Body = bodyBuilder.ToString(),
            IsBodyHtml = false
        };

        if (!string.IsNullOrWhiteSpace(replyTo))
        {
            message.ReplyToList.Add(new MailAddress(replyTo));
        }

        var attachmentStream = new MemoryStream(pdfBytes, writable: false);
        var attachment = new Attachment(attachmentStream, BuildAttachmentFileName(workstation), "application/pdf");
        message.Attachments.Add(attachment);

        return message;
    }

    private static string BuildAttachmentFileName(Workstation workstation)
    {
        var sanitizedName = string.Join("-", new[]
        {
            workstation.InventoryNumber,
            workstation.Name
        }.Where(NotNullOrWhiteSpace)).ToLowerInvariant();

        return string.IsNullOrWhiteSpace(sanitizedName)
            ? "handover.pdf"
            : $"handover-{sanitizedName}.pdf";
    }

    private static byte[] GeneratePdf(Workstation workstation, WorkstationHandoverOptions options)
    {
        using var stream = new MemoryStream();

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header()
                    .AlignCenter()
                    .Text("Předávací protokol pracovního zařízení")
                    .SemiBold()
                    .FontSize(20);

                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text(text =>
                    {
                        text.Span("Zařízení: ").SemiBold();
                        text.Span($"{workstation.Name} ({workstation.InventoryNumber})");
                    });
                    column.Item().Text(text =>
                    {
                        text.Span("Datum předání: ").SemiBold();
                        text.Span(options.HandoverAtUtc.ToString("dd.MM.yyyy HH:mm"));
                    });
                    column.Item().Text(text =>
                    {
                        text.Span("Předal: ").SemiBold();
                        text.Span(options.Actor ?? "N/A");
                    });

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellHeader).Text("Původní stav");
                            header.Cell().Element(CellHeader).Text("Nový stav");
                        });

                        table.Cell().Element(CellBody).Column(column =>
                        {
                            column.Item().Text(text =>
                            {
                                text.Span("Uživatel: ").SemiBold();
                                text.Span(options.OldOwnerDisplayName ?? "Neuvedeno");
                            });
                            column.Item().Text(text =>
                            {
                                text.Span("Oddělení: ").SemiBold();
                                text.Span(options.OldOwnerDepartment ?? "Neuvedeno");
                            });
                            column.Item().Text(text =>
                            {
                                text.Span("Umístění: ").SemiBold();
                                text.Span(options.OldLocationName ?? options.OldLocationId.ToString());
                            });
                            if (!string.IsNullOrWhiteSpace(options.OldLocationNote))
                            {
                                column.Item().Text(text =>
                                {
                                    text.Span("Poznámka: ").SemiBold();
                                    text.Span(options.OldLocationNote);
                                });
                            }
                        });

                        table.Cell().Element(CellBody).Column(column =>
                        {
                            column.Item().Text(text =>
                            {
                                text.Span("Uživatel: ").SemiBold();
                                text.Span(options.NewOwnerDisplayName ?? "Neuvedeno");
                            });
                            column.Item().Text(text =>
                            {
                                text.Span("Oddělení: ").SemiBold();
                                text.Span(options.NewOwnerDepartment ?? "Neuvedeno");
                            });
                            column.Item().Text(text =>
                            {
                                text.Span("Umístění: ").SemiBold();
                                text.Span(options.NewLocationName ?? options.NewLocationId.ToString());
                            });
                            if (!string.IsNullOrWhiteSpace(options.NewLocationNote))
                            {
                                column.Item().Text(text =>
                                {
                                    text.Span("Poznámka: ").SemiBold();
                                    text.Span(options.NewLocationNote);
                                });
                            }
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(options.Comment))
                    {
                        column.Item().Text(text =>
                        {
                            text.Span("Komentář: ").SemiBold();
                            text.Span(options.Comment);
                        });
                    }

                    column.Item().LineHorizontal(0.5f);
                    column.Item().PaddingTop(20).Row(row =>
                    {
                        row.RelativeColumn().Stack(stack =>
                        {
                            stack.Spacing(6);
                            stack.Item().Text("Předávající").SemiBold();
                            stack.Item().LineHorizontal(0.5f);
                        });
                        row.RelativeColumn().Stack(stack =>
                        {
                            stack.Spacing(6);
                            stack.Item().Text("Přebírající").SemiBold();
                            stack.Item().LineHorizontal(0.5f);
                        });
                    });
                });

                page.Footer()
                    .AlignRight()
                    .Text($"Generováno: {DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC");
            });
        }).GeneratePdf(stream);

        return stream.ToArray();
    }

    private static IContainer CellHeader(IContainer container)
        => container.Padding(5).Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Lighten2);

    private static IContainer CellBody(IContainer container)
        => container.Padding(5).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3);

    private static bool NotNullOrWhiteSpace(string? value) => !string.IsNullOrWhiteSpace(value);

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
}
