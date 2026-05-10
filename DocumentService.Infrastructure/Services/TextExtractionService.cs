using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentService.Application.Abstractions;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace DocumentService.Infrastructure.Services;

public partial class TextExtractionService : ITextExtractionService
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
        LogExtractingText(_logger, contentType);

        var extractedText = contentType.ToLowerInvariant() switch
        {
            "application/pdf" => ExtractFromPdf(fileStream),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ExtractFromDocx(fileStream),
            "text/plain" => await ExtractFromTxt(fileStream).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Content type '{contentType}' is not supported for text extraction.")
        };

        return CleanAndNormalizeText(extractedText);
    }

    private string ExtractFromPdf(Stream fileStream)
    {
        try
        {
            using var document = PdfDocument.Open(fileStream);
            var sb = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                var words = page.GetWords().Select(w => w.Text);
                var pageText = string.Join(" ", words);

                sb.AppendLine(pageText);
            }

            var text = sb.ToString();
            LogPdfExtracted(_logger, text.Length, document.NumberOfPages);
            return text;
        }
        catch (Exception ex)
        {
            LogPdfFailure(_logger, ex);
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

            foreach (var element in body.Descendants<Paragraph>())
            {
                foreach (var textNode in element.Descendants().OfType<Text>())
                {
                    sb.Append(textNode.Text);
                }
                foreach (var _ in element.Descendants().OfType<Break>())
                {
                    sb.AppendLine();
                }
                foreach (var _ in element.Descendants().OfType<TabChar>())
                {
                    sb.Append('\t');
                }
                sb.AppendLine();
            }

            var text = sb.ToString();
            LogDocxExtracted(_logger, text.Length);
            return text;
        }
        catch (Exception ex)
        {
            LogDocxFailure(_logger, ex);
            throw new InvalidOperationException("Failed to extract text from DOCX file.", ex);
        }
    }

    private static async Task<string> ExtractFromTxt(Stream fileStream)
    {
        using var reader = new StreamReader(fileStream, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync().ConfigureAwait(false);
    }


    private static string CleanAndNormalizeText(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var text = HyphenLineBreakRegex().Replace(input, "$1$2");
        text = InlineWhitespaceRegex().Replace(text, " ");
        text = ExcessNewlinesRegex().Replace(text, "\n\n");

        return text.Trim();
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Extracting text from file with content type: {ContentType}")]
    private static partial void LogExtractingText(ILogger logger, string contentType);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Extracted {CharCount} characters from PDF ({PageCount} pages)")]
    private static partial void LogPdfExtracted(ILogger logger, int charCount, int pageCount);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Failed to extract text from PDF")]
    private static partial void LogPdfFailure(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Extracted {CharCount} characters from DOCX")]
    private static partial void LogDocxExtracted(ILogger logger, int charCount);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Failed to extract text from DOCX")]
    private static partial void LogDocxFailure(ILogger logger, Exception exception);

    [GeneratedRegex(@"(\w+)[-‐‑]\s*[\r\n]+\s*(\w+)")]
    private static partial Regex HyphenLineBreakRegex();

    [GeneratedRegex(@"[^\S\r\n]+")]
    private static partial Regex InlineWhitespaceRegex();

    [GeneratedRegex(@"(\r?\n){3,}")]
    private static partial Regex ExcessNewlinesRegex();
}