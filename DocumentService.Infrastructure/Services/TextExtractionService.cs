using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentService.Application.Abstractions;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace DocumentService.Infrastructure.Services;

public class TextExtractionService : ITextExtractionService
{
    private static readonly string[] SupportedContentTypes =
    [
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain"
    ];

    private readonly ILogger<TextExtractionService> _logger;

    public TextExtractionService(ILogger<TextExtractionService> logger)
    {
        _logger = logger;
    }

    public bool SupportsContentType(string contentType)
    {
        return SupportedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string> ExtractTextAsync(Stream fileStream, string contentType)
    {
        _logger.LogInformation("Extracting text from file with content type: {ContentType}", contentType);

        return contentType.ToLowerInvariant() switch
        {
            "application/pdf" => ExtractFromPdf(fileStream),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ExtractFromDocx(fileStream),
            "text/plain" => await ExtractFromTxt(fileStream),
            _ => throw new NotSupportedException($"Content type '{contentType}' is not supported for text extraction.")
        };
    }

    private string ExtractFromPdf(Stream fileStream)
    {
        try
        {
            using var document = PdfDocument.Open(fileStream);
            var sb = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                sb.AppendLine(page.Text);
            }

            var text = sb.ToString().Trim();
            _logger.LogInformation("Extracted {CharCount} characters from PDF ({PageCount} pages)", text.Length, document.NumberOfPages);
            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from PDF");
            throw new InvalidOperationException("Failed to extract text from PDF file.", ex);
        }
    }

    private string ExtractFromDocx(Stream fileStream)
    {
        try
        {
            using var document = WordprocessingDocument.Open(fileStream, false);
            var body = document.MainDocumentPart?.Document?.Body;

            if (body is null)
                return string.Empty;

            var sb = new StringBuilder();

            foreach (var paragraph in body.Elements<Paragraph>())
            {
                sb.AppendLine(paragraph.InnerText);
            }

            var text = sb.ToString().Trim();
            _logger.LogInformation("Extracted {CharCount} characters from DOCX", text.Length);
            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from DOCX");
            throw new InvalidOperationException("Failed to extract text from DOCX file.", ex);
        }
    }

    private static async Task<string> ExtractFromTxt(Stream fileStream)
    {
        using var reader = new StreamReader(fileStream, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}

