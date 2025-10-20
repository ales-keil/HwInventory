using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HWInventory.Application.Abstractions;

public interface IHandoverConfigurationStore
{
    Task<HandoverConfigurationModel> GetAsync(CancellationToken cancellationToken = default);

    Task<HandoverConfigurationModel> SaveAsync(HandoverConfigurationModel configuration, CancellationToken cancellationToken = default);
}

public record HandoverConfigurationModel(
    IReadOnlyList<string> DefaultTo,
    IReadOnlyList<string> DefaultCc,
    IReadOnlyList<string> DefaultBcc,
    string? DefaultSubject,
    string? DefaultBody,
    string? PdfLogoBase64,
    bool UseMinimalPdf,
    string? PdfFooterNote);
