using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using UglyToad.PdfPig;

namespace ServicesLayer;

public interface IDocumentTextExtractor
{
    Task<string> ExtractAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);
}

public sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    public Task<string> ExtractAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var text = extension switch
        {
            ".pdf" => ExtractPdf(stream),
            ".docx" => ExtractDocx(stream),
            ".pptx" => ExtractPptx(stream),
            ".txt" => ExtractPlainText(stream),
            _ => throw new InvalidOperationException("Only PDF, DOCX, PPTX, and TXT files are supported.")
        };

        return Task.FromResult(NormalizeWhitespace(text));
    }

    private static string ExtractPdf(Stream stream)
    {
        using var document = PdfDocument.Open(stream);
        var builder = new StringBuilder();
        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return builder.ToString();
    }

    private static string ExtractDocx(Stream stream)
    {
        using var document = WordprocessingDocument.Open(stream, false);
        return document.MainDocumentPart?.Document.Body?.InnerText ?? string.Empty;
    }

    private static string ExtractPptx(Stream stream)
    {
        using var document = PresentationDocument.Open(stream, false);
        var builder = new StringBuilder();
        var presentationPart = document.PresentationPart;
        if (presentationPart?.Presentation.SlideIdList is null)
        {
            return string.Empty;
        }

        foreach (var slideId in presentationPart.Presentation.SlideIdList.Elements<SlideId>())
        {
            var relationshipId = slideId.RelationshipId?.Value;
            if (string.IsNullOrWhiteSpace(relationshipId))
            {
                continue;
            }

            var slidePart = (SlidePart)presentationPart.GetPartById(relationshipId);
            builder.AppendLine(slidePart.Slide.InnerText);
        }

        return builder.ToString();
    }

    private static string ExtractPlainText(Stream stream)
    {
        return TextEncodingHelper.Decode(stream);
    }

    private static string NormalizeWhitespace(string text)
    {
        return string.Join(Environment.NewLine, text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line)));
    }
}
