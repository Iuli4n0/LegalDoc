using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        var extractedText = contentType.ToLowerInvariant() switch
        {
            "application/pdf" => ExtractFromPdf(fileStream),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ExtractFromDocx(fileStream),
            "text/plain" => await ExtractFromTxt(fileStream),
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
            
            foreach (var element in body.Descendants())
            {
                if (element is Paragraph)
                {
                    foreach (var child in element.Descendants())
                    {
                        if (child is Text textNode)
                            sb.Append(textNode.Text);
                        else if (child is Break)
                            sb.AppendLine();
                        else if (child is TabChar)
                            sb.Append('\t');
                    }
                    sb.AppendLine(); 
                }
            }

            var text = sb.ToString();
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

   
    private static string CleanAndNormalizeText(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

       
        var text = Regex.Replace(input, @"(\w+)[-‐‑]\s*[\r\n]+\s*(\w+)", "$1$2");
        
        text = Regex.Replace(text, @"[^\S\r\n]+", " ");
        
        text = Regex.Replace(text, @"(\r?\n){3,}", "\n\n");

        return text.Trim();
    }
}