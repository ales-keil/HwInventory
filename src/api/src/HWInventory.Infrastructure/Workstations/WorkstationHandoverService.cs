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
using QuestPDF.Drawing;
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
        var tokens = BuildTemplateTokens(workstation, options);
        var resolvedSubject = ApplyTemplate(options.Subject, tokens);
        var resolvedBody = ApplyTemplate(options.MessageBody, tokens);
        var resolvedComment = ApplyTemplate(options.Comment, tokens);

        var effectiveOptions = options;
        if (!string.Equals(resolvedComment, options.Comment, StringComparison.Ordinal))
        {
            effectiveOptions = effectiveOptions with { Comment = resolvedComment };
        }

        var pdfBytes = GeneratePdf(workstation, effectiveOptions);

        var recipients = options.To?.Where(NotNullOrWhiteSpace).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
        var ccRecipients = options.Cc?.Where(NotNullOrWhiteSpace).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
        var bccRecipients = options.Bcc?.Where(NotNullOrWhiteSpace).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();

        if (recipients.Length == 0)
        {
            _logger.LogWarning("Workstation handover triggered without recipients (WorkstationId: {WorkstationId}).", workstation.Id);
            return new WorkstationHandoverResult(false, "No e-mail recipients were provided.", pdfBytes, null, false);
        }

        var connector = await _dbContext.ConnectorProfiles
            .Include(x => x.Secrets)
            .FirstOrDefaultAsync(x => x.Type == ConnectorType && x.Enabled, cancellationToken);

        if (connector is null)
        {
            _logger.LogWarning("SMTP connector is not configured – skipping handover e-mail for workstation {WorkstationId}", workstation.Id);
            return new WorkstationHandoverResult(false, "SMTP connector is not configured.", pdfBytes, null, false);
        }

        var configuration = ParseConfiguration(connector.ConfigurationJson);
        if (!configuration.TryGetValue("host", out var host) || string.IsNullOrWhiteSpace(host))
        {
            _logger.LogWarning("SMTP connector is missing host – skipping handover e-mail for workstation {WorkstationId}", workstation.Id);
            return new WorkstationHandoverResult(false, "SMTP host is not configured.", pdfBytes, null, false);
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

            using var message = BuildMailMessage(workstation, effectiveOptions, fromAddress, replyTo, pdfBytes, resolvedSubject, resolvedBody);

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
            return new WorkstationHandoverResult(true, "Handover e-mail sent successfully.", pdfBytes, null, options.AcceptUrl != null && options.DeclineUrl != null && !options.Force);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send handover e-mail for workstation {WorkstationId}", workstation.Id);
            return new WorkstationHandoverResult(false, $"Failed to send e-mail: {ex.Message}", pdfBytes, null, options.AcceptUrl != null && options.DeclineUrl != null && !options.Force);
        }
    }

    private static MailMessage BuildMailMessage(
        Workstation workstation,
        WorkstationHandoverOptions options,
        string fromAddress,
        string? replyTo,
        byte[] pdfBytes,
        string? resolvedSubject,
        string? resolvedBody)
    {
        var subject = string.IsNullOrWhiteSpace(resolvedSubject)
            ? $"Předávací protokol – {workstation.Name} ({workstation.InventoryNumber})"
            : resolvedSubject!;

        var textBuilder = new StringBuilder();
        var htmlBuilder = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(resolvedBody))
        {
            var sanitized = resolvedBody!.Trim();
            textBuilder.AppendLine(sanitized);
            textBuilder.AppendLine();
            htmlBuilder.AppendLine($"<p>{System.Net.WebUtility.HtmlEncode(sanitized).Replace("\n", "<br />")}</p>");
        }

        AppendSection(textBuilder, htmlBuilder, "Zařízení", $"{workstation.Name} ({workstation.InventoryNumber})");
        AppendSection(textBuilder, htmlBuilder, "Předáno dne", options.HandoverAtUtc.ToString("dd.MM.yyyy HH:mm"));
        AppendSection(textBuilder, htmlBuilder, "Předal", options.Actor ?? "N/A");

        AppendBlock(textBuilder, htmlBuilder, "Původní stav", new (string Label, string? Value)[]
        {
            ("Uživatel", options.OldOwnerDisplayName ?? "Neuvedeno"),
            ("Oddělení", options.OldOwnerDepartment ?? "Neuvedeno"),
            ("Umístění", options.OldLocationName ?? options.OldLocationId.ToString()),
            ("Poznámka", options.OldLocationNote)
        });

        AppendBlock(textBuilder, htmlBuilder, "Nový stav", new (string Label, string? Value)[]
        {
            ("Uživatel", options.NewOwnerDisplayName ?? "Neuvedeno"),
            ("Oddělení", options.NewOwnerDepartment ?? "Neuvedeno"),
            ("Umístění", options.NewLocationName ?? options.NewLocationId.ToString()),
            ("Poznámka", options.NewLocationNote)
        });

        if (!string.IsNullOrWhiteSpace(options.Comment))
        {
            AppendSection(textBuilder, htmlBuilder, "Komentář", options.Comment);
        }

        if (!options.Force && options.AcceptUrl is not null && options.DeclineUrl is not null)
        {
            textBuilder.AppendLine();
            textBuilder.AppendLine($"Přijmout předání: {options.AcceptUrl}");
            textBuilder.AppendLine($"Odmítnout předání: {options.DeclineUrl}");

            htmlBuilder.AppendLine("<p>");
            htmlBuilder.AppendLine($"  <a href=\"{options.AcceptUrl}\" style=\"background-color:#16a34a;color:#ffffff;padding:10px 16px;border-radius:6px;text-decoration:none;font-weight:600;margin-right:12px;display:inline-block\">Přijmout</a>");
            htmlBuilder.AppendLine($"  <a href=\"{options.DeclineUrl}\" style=\"background-color:#dc2626;color:#ffffff;padding:10px 16px;border-radius:6px;text-decoration:none;font-weight:600;display:inline-block\">Odmítnout</a>");
            htmlBuilder.AppendLine("</p>");
            htmlBuilder.AppendLine($"<p style=\"margin-top:16px;font-size:13px;color:#475569\">Pokud tlačítka nefungují, použijte odkazy:<br /><a href=\"{options.AcceptUrl}\">{options.AcceptUrl}</a><br /><a href=\"{options.DeclineUrl}\">{options.DeclineUrl}</a></p>");
        }

        if (options.Force)
        {
            htmlBuilder.AppendLine("<p style=\"margin-top:16px;color:#475569\"><strong>Upozornění:</strong> Tento převod provedl administrátor bez potvrzení příjemce.</p>");
            textBuilder.AppendLine();
            textBuilder.AppendLine("Upozornění: Převod provedl administrátor bez potvrzení příjemce.");
        }

        var message = new MailMessage
        {
            From = new MailAddress(fromAddress),
            Subject = subject,
            Body = textBuilder.ToString(),
            IsBodyHtml = false
        };

        var htmlBody = $"<html><body style=\"font-family:Segoe UI,Arial,sans-serif;line-height:1.5;color:#0f172a\">{htmlBuilder}</body></html>";
        var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, "text/html");
        message.AlternateViews.Add(htmlView);

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

    private static void AppendSection(StringBuilder textBuilder, StringBuilder htmlBuilder, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var encoded = System.Net.WebUtility.HtmlEncode(value);
        textBuilder.AppendLine($"{label}: {value}");
        htmlBuilder.AppendLine($"<p><strong>{System.Net.WebUtility.HtmlEncode(label)}:</strong> {encoded}</p>");
    }

    private static void AppendBlock(StringBuilder textBuilder, StringBuilder htmlBuilder, string title, IEnumerable<(string Label, string? Value)> rows)
    {
        textBuilder.AppendLine();
        textBuilder.AppendLine(title + ":");
        htmlBuilder.AppendLine($"<h3 style=\"margin-top:24px;margin-bottom:8px;font-size:16px;color:#1e293b\">{System.Net.WebUtility.HtmlEncode(title)}</h3>");
        htmlBuilder.AppendLine("<ul style=\"margin:0 0 12px 20px;padding:0;\">");

        foreach (var (label, value) in rows)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            textBuilder.AppendLine($"  {label}: {value}");
            htmlBuilder.AppendLine($"  <li><strong>{System.Net.WebUtility.HtmlEncode(label)}:</strong> {System.Net.WebUtility.HtmlEncode(value)}</li>");
        }

        htmlBuilder.AppendLine("</ul>");
    }

    private static byte[] GeneratePdf(Workstation workstation, WorkstationHandoverOptions options)
    {
        using var stream = new MemoryStream();
        var logoBytes = DecodeLogo(options.PdfLogoBase64);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(12));

                page.Header().Column(header =>
                {
                    header.Spacing(6);

                    if (logoBytes is not null)
                    {
                        header.Item()
                            .AlignCenter()
                            .Height(60)
                            .Image(logoBytes, ImageScaling.FitArea);
                    }

                    header.Item()
                        .AlignCenter()
                        .Text("Předávací protokol pracovního zařízení")
                        .SemiBold()
                        .FontSize(20);
                });

                page.Content().Column(column =>
                {
                    column.Spacing(12);
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

                    if (options.UseMinimalPdf)
                    {
                        column.Item().Column(section =>
                        {
                            section.Spacing(6);
                            section.Item().Text("Shrnutí předání").SemiBold().FontSize(14);
                            section.Item().Text(text =>
                            {
                                text.Span("Původní umístění: ").SemiBold();
                                text.Span(options.OldLocationName ?? options.OldLocationId.ToString());
                            });
                            section.Item().Text(text =>
                            {
                                text.Span("Nové umístění: ").SemiBold();
                                text.Span(options.NewLocationName ?? options.NewLocationId.ToString());
                            });
                            section.Item().Text(text =>
                            {
                                text.Span("Původní uživatel: ").SemiBold();
                                text.Span(options.OldOwnerDisplayName ?? "Neuvedeno");
                            });
                            section.Item().Text(text =>
                            {
                                text.Span("Nový uživatel: ").SemiBold();
                                text.Span(options.NewOwnerDisplayName ?? "Neuvedeno");
                            });
                            if (!string.IsNullOrWhiteSpace(options.Comment))
                            {
                                section.Item().Text(text =>
                                {
                                    text.Span("Poznámka: ").SemiBold();
                                    text.Span(options.Comment);
                                });
                            }
                        });
                    }
                    else
                    {
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

                            table.Cell().Element(CellBody).Column(col =>
                            {
                                col.Item().Text(text =>
                                {
                                    text.Span("Uživatel: ").SemiBold();
                                    text.Span(options.OldOwnerDisplayName ?? "Neuvedeno");
                                });
                                col.Item().Text(text =>
                                {
                                    text.Span("Oddělení: ").SemiBold();
                                    text.Span(options.OldOwnerDepartment ?? "Neuvedeno");
                                });
                                col.Item().Text(text =>
                                {
                                    text.Span("Umístění: ").SemiBold();
                                    text.Span(options.OldLocationName ?? options.OldLocationId.ToString());
                                });
                                if (!string.IsNullOrWhiteSpace(options.OldLocationNote))
                                {
                                    col.Item().Text(text =>
                                    {
                                        text.Span("Poznámka: ").SemiBold();
                                        text.Span(options.OldLocationNote);
                                    });
                                }
                            });

                            table.Cell().Element(CellBody).Column(col =>
                            {
                                col.Item().Text(text =>
                                {
                                    text.Span("Uživatel: ").SemiBold();
                                    text.Span(options.NewOwnerDisplayName ?? "Neuvedeno");
                                });
                                col.Item().Text(text =>
                                {
                                    text.Span("Oddělení: ").SemiBold();
                                    text.Span(options.NewOwnerDepartment ?? "Neuvedeno");
                                });
                                col.Item().Text(text =>
                                {
                                    text.Span("Umístění: ").SemiBold();
                                    text.Span(options.NewLocationName ?? options.NewLocationId.ToString());
                                });
                                if (!string.IsNullOrWhiteSpace(options.NewLocationNote))
                                {
                                    col.Item().Text(text =>
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

                page.Footer().Column(footer =>
                {
                    footer.Spacing(4);
                    if (!string.IsNullOrWhiteSpace(options.PdfFooterNote))
                    {
                        footer.Item()
                            .AlignLeft()
                            .Text(options.PdfFooterNote)
                            .FontSize(10)
                            .Italic();
                    }

                    footer.Item()
                        .AlignRight()
                        .Text($"Generováno: {DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC")
                        .FontSize(9);
                });
            });
        }).GeneratePdf(stream);

        return stream.ToArray();
    }

    private static IContainer CellHeader(IContainer container)
        => container.Padding(5).Background(Colors.Grey.Lighten3).Border(0.5f).BorderColor(Colors.Grey.Lighten2);

    private static IContainer CellBody(IContainer container)
        => container.Padding(5).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3);

    private static bool NotNullOrWhiteSpace(string? value) => !string.IsNullOrWhiteSpace(value);

    private static IReadOnlyDictionary<string, string?> BuildTemplateTokens(Workstation workstation, WorkstationHandoverOptions options)
    {
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["{AssetTag}"] = workstation.InventoryNumber,
            ["{Name}"] = workstation.Name,
            ["{OldLocation}"] = options.OldLocationName ?? options.OldLocationId.ToString(),
            ["{NewLocation}"] = options.NewLocationName ?? options.NewLocationId.ToString(),
            ["{OldOwner}"] = options.OldOwnerDisplayName,
            ["{NewOwner}"] = options.NewOwnerDisplayName,
            ["{OldDepartment}"] = options.OldOwnerDepartment,
            ["{NewDepartment}"] = options.NewOwnerDepartment,
            ["{Date}"] = options.HandoverAtUtc.ToString("dd.MM.yyyy HH:mm"),
            ["{Actor}"] = options.Actor,
            ["{Comment}"] = options.Comment
        };
    }

    private static string? ApplyTemplate(string? template, IReadOnlyDictionary<string, string?> tokens)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return template;
        }

        var result = template;
        foreach (var (key, value) in tokens)
        {
            result = result.Replace(key, value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }

    private static byte[]? DecodeLogo(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(base64);
        }
        catch
        {
            return null;
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
}
