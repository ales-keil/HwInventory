using HWInventory.Domain.Entities;

namespace HWInventory.Application.Abstractions;

public interface ILabelRenderingService
{
    string RenderZpl(LabelTemplate template, IDictionary<string, string> data);
    byte[] RenderPdf(LabelTemplate template, IDictionary<string, string> data);
}
