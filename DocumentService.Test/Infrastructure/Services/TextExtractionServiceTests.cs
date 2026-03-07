using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentService.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentService.Test.Infrastructure.Services;

public class TextExtractionServiceTests
{
    private readonly TextExtractionService _service = new(new Mock<ILogger<TextExtractionService>>().Object);

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("APPLICATION/PDF")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("text/plain")]
    public void Given_SupportedType_When_SupportsContentType_Then_ReturnsTrue(string contentType)
    {
        var result = _service.SupportsContentType(contentType);

        Assert.True(result);
    }

    [Fact]
    public void Given_UnsupportedType_When_SupportsContentType_Then_ReturnsFalse()
    {
        var result = _service.SupportsContentType("image/png");

        Assert.False(result);
    }

    [Fact]
    public async Task Given_TextPlain_When_ExtractTextAsync_Then_TextIsReturned()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello txt"));

        var result = await _service.ExtractTextAsync(stream, "text/plain");

        Assert.Equal("hello txt", result);
    }

    [Fact]
    public async Task Given_Docx_When_ExtractTextAsync_Then_ParagraphTextIsReturned()
    {
        await using var stream = new MemoryStream();

        using (var document = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(
                new Body(
                    new Paragraph(new Run(new Text("Line one"))),
                    new Paragraph(new Run(new Text("Line two")))));
        }

        stream.Position = 0;

        var result = await _service.ExtractTextAsync(
            stream,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        Assert.Contains("Line one", result);
        Assert.Contains("Line two", result);
    }

    [Fact]
    public async Task Given_DocxWithoutBody_When_ExtractTextAsync_Then_EmptyStringIsReturned()
    {
        await using var stream = new MemoryStream();

        using (var document = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, true))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document();
        }

        stream.Position = 0;

        var result = await _service.ExtractTextAsync(
            stream,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task Given_InvalidDocx_When_ExtractTextAsync_Then_InvalidOperationExceptionIsThrown()
    {
        await using var stream = new MemoryStream([1, 2, 3, 4]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExtractTextAsync(stream, "application/vnd.openxmlformats-officedocument.wordprocessingml.document"));

        Assert.Contains("DOCX", exception.Message);
    }

    [Fact]
    public async Task Given_Pdf_When_ExtractTextAsync_Then_TextIsReturned()
    {
        var bytes = CreateSimplePdf("Hello PDF");
        await using var stream = new MemoryStream(bytes);

        var result = await _service.ExtractTextAsync(stream, "application/pdf");

        Assert.Contains("Hello PDF", result);
    }

    [Fact]
    public async Task Given_InvalidPdf_When_ExtractTextAsync_Then_InvalidOperationExceptionIsThrown()
    {
        await using var stream = new MemoryStream([1, 2, 3, 4]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ExtractTextAsync(stream, "application/pdf"));

        Assert.Contains("PDF", exception.Message);
    }

    [Fact]
    public async Task Given_UnsupportedType_When_ExtractTextAsync_Then_NotSupportedExceptionIsThrown()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("x"));

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() =>
            _service.ExtractTextAsync(stream, "image/png"));

        Assert.Contains("not supported", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] CreateSimplePdf(string text)
    {
        var escapedText = text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
        var content = $"BT /F1 24 Tf 100 700 Td ({escapedText}) Tj ET";

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"
        };

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);

        writer.Write("%PDF-1.4\n");

        var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(stream.Position);
            writer.Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        writer.Flush();
        var xrefOffset = stream.Position;

        writer.Write($"xref\n0 {objects.Count + 1}\n");
        writer.Write("0000000000 65535 f \n");

        for (var i = 1; i < offsets.Count; i++)
        {
            writer.Write($"{offsets[i]:D10} 00000 n \n");
        }

        writer.Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
        writer.Flush();

        return stream.ToArray();
    }
}
