using System.Collections.Generic;

namespace HWInventory.Api.Models;

public record HandoverConfigurationResponse(
    IReadOnlyList<string> DefaultTo,
    IReadOnlyList<string> DefaultCc,
    IReadOnlyList<string> DefaultBcc,
    string? DefaultSubject,
    string? DefaultBody,
    string? PdfLogoBase64,
    bool UseMinimalPdf,
    string? PdfFooterNote);

public record HandoverConfigurationRequest(
    IReadOnlyList<string>? DefaultTo,
    IReadOnlyList<string>? DefaultCc,
    IReadOnlyList<string>? DefaultBcc,
    string? DefaultSubject,
    string? DefaultBody,
    string? PdfLogoBase64,
    bool UseMinimalPdf,
    string? PdfFooterNote);
