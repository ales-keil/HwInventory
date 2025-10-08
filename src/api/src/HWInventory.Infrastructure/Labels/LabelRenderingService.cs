using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HWInventory.Application.Abstractions;
using HWInventory.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HWInventory.Infrastructure.Labels;

public class LabelRenderingService : ILabelRenderingService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string RenderZpl(LabelTemplate template, IDictionary<string, string> data)
    {
        var model = ParsePayload(template);
        var builder = new StringBuilder();
        builder.AppendLine("^XA");
        builder.AppendLine("^CI28");
        builder.AppendLine($"^PW{ToDots(model.WidthMm, model.Dpi)}");
        builder.AppendLine($"^LL{ToDots(model.HeightMm, model.Dpi)}");

        foreach (var element in model.Elements)
        {
            var x = ToDots(element.X, model.Dpi);
            var y = ToDots(element.Y, model.Dpi);
            var value = ResolveValue(element, data);

            switch (element.Type)
            {
                case LabelElementType.Text:
                    builder.AppendLine($"^FO{x},{y}^A0N,{element.FontSize},{element.FontSize}^FD{Escape(value)}^FS");
                    break;
                case LabelElementType.Barcode128:
                    builder.AppendLine($"^FO{x},{y}^BCN,{ToDots(element.HeightMm, model.Dpi)},Y,N,N^FD{Escape(value)}^FS");
                    break;
                case LabelElementType.Qr:
                    builder.AppendLine($"^FO{x},{y}^BQN,2,10^FDLA,{Escape(value)}^FS");
                    break;
            }
        }

        builder.AppendLine("^XZ");
        return builder.ToString();
    }

    public byte[] RenderPdf(LabelTemplate template, IDictionary<string, string> data)
    {
        var model = ParsePayload(template);
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(10);
                page.Size(model.WidthMm * 2.83465f, model.HeightMm * 2.83465f);
                page.Content().Canvas((canvas, size) =>
                {
                    foreach (var element in model.Elements)
                    {
                        var value = ResolveValue(element, data);
                        var x = element.X * 2.83465f;
                        var y = element.Y * 2.83465f;

                        switch (element.Type)
                        {
                            case LabelElementType.Text:
                                canvas.DrawText(value, TextStyle.Default.FontSize(element.FontSize), new Position(x, y));
                                break;
                            case LabelElementType.Barcode128:
                                canvas.DrawRectangle(new Position(x, y), new Size(element.WidthMm * 2.83465f, element.HeightMm * 2.83465f));
                                canvas.DrawText($"[Code128] {value}", TextStyle.Default.FontSize(8), new Position(x, y + 8));
                                break;
                            case LabelElementType.Qr:
                                canvas.DrawRectangle(new Position(x, y), new Size(element.WidthMm * 2.83465f, element.HeightMm * 2.83465f));
                                canvas.DrawText($"[QR] {value}", TextStyle.Default.FontSize(8), new Position(x, y + 8));
                                break;
                        }
                    }
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    private static LabelTemplateModel ParsePayload(LabelTemplate template)
    {
        if (string.IsNullOrWhiteSpace(template.Payload))
        {
            throw new InvalidOperationException("Label template payload cannot be empty");
        }

        var model = JsonSerializer.Deserialize<LabelTemplateModel>(template.Payload, SerializerOptions);
        if (model is null)
        {
            throw new InvalidOperationException("Label template payload is invalid");
        }

        return model;
    }

    private static string ResolveValue(LabelElement element, IDictionary<string, string> data)
    {
        if (!string.IsNullOrWhiteSpace(element.StaticValue))
        {
            return element.StaticValue!;
        }

        if (!string.IsNullOrWhiteSpace(element.DataKey) && data.TryGetValue(element.DataKey!, out var value))
        {
            return value;
        }

        return string.Empty;
    }

    private static string Escape(string value) => value
        .Replace("^", " ")
        .Replace("~", " ");

    private static int ToDots(float millimeters, int dpi)
        => (int)Math.Round(millimeters / 25.4f * dpi);

    private class LabelTemplateModel
    {
        public int Dpi { get; set; } = 203;
        public float WidthMm { get; set; } = 50;
        public float HeightMm { get; set; } = 30;
        public IList<LabelElement> Elements { get; set; } = new List<LabelElement>();
    }

    private class LabelElement
    {
        public LabelElementType Type { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float WidthMm { get; set; } = 30;
        public float HeightMm { get; set; } = 10;
        public int FontSize { get; set; } = 12;
        public string? StaticValue { get; set; }
        public string? DataKey { get; set; }
    }

    private enum LabelElementType
    {
        Text,
        Barcode128,
        Qr
    }
}
