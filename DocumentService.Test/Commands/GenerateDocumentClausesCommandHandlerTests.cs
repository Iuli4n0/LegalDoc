using DocumentService.Application.Abstractions;
using DocumentService.Application.Commands.GenerateDocumentClauses;
using DocumentService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentService.Test.Commands;

public class GenerateDocumentClausesCommandHandlerTests
{
    private readonly Mock<IDocumentRepository> _documentRepositoryMock;
    private readonly Mock<IClauseRepository> _clauseRepositoryMock;
    private readonly Mock<IFileStorageService> _fileStorageServiceMock;
    private readonly Mock<ITextExtractionService> _textExtractionServiceMock;
    private readonly Mock<IClauseExtractorService> _clauseExtractorServiceMock;
    private readonly Mock<ILogger<GenerateDocumentClausesCommandHandler>> _loggerMock;
    private readonly GenerateDocumentClausesCommandHandler _handler;

    public GenerateDocumentClausesCommandHandlerTests()
    {
        _documentRepositoryMock = new Mock<IDocumentRepository>();
        _clauseRepositoryMock = new Mock<IClauseRepository>();
        _fileStorageServiceMock = new Mock<IFileStorageService>();
        _textExtractionServiceMock = new Mock<ITextExtractionService>();
        _clauseExtractorServiceMock = new Mock<IClauseExtractorService>();
        _loggerMock = new Mock<ILogger<GenerateDocumentClausesCommandHandler>>();

        _handler = new GenerateDocumentClausesCommandHandler(
            _documentRepositoryMock.Object,
            _clauseRepositoryMock.Object,
            _fileStorageServiceMock.Object,
            _textExtractionServiceMock.Object,
            _clauseExtractorServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Given_NonExistentDocumentId_When_HandleIsCalled_Then_InvalidOperationExceptionIsThrown()
    {
        var documentId = Guid.NewGuid();
        var command = new GenerateDocumentClausesCommand(documentId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId))
            .ReturnsAsync((Document?)null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains(documentId.ToString(), exception.Message);
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task Given_DocumentWithUnsupportedContentType_When_HandleIsCalled_Then_NotSupportedExceptionIsThrown()
    {
        var documentId = Guid.NewGuid();
        var document = Document.Create("image.png", "image/png", "key/image.png", 5000, "user-1");
        var command = new GenerateDocumentClausesCommand(documentId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId))
            .ReturnsAsync(document);

        _textExtractionServiceMock
            .Setup(s => s.SupportsContentType("image/png"))
            .Returns(false);

        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("image/png", exception.Message);
    }

    [Fact]
    public async Task Given_DocumentWithEmptyExtractedText_When_HandleIsCalled_Then_InvalidOperationExceptionIsThrown()
    {
        var documentId = Guid.NewGuid();
        var document = Document.Create("empty.pdf", "application/pdf", "key/empty.pdf", 100, "user-1");
        var command = new GenerateDocumentClausesCommand(documentId);
        var fileStream = new MemoryStream();

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId))
            .ReturnsAsync(document);

        _textExtractionServiceMock
            .Setup(s => s.SupportsContentType("application/pdf"))
            .Returns(true);

        _fileStorageServiceMock
            .Setup(s => s.DownloadFileAsync("key/empty.pdf"))
            .ReturnsAsync(fileStream);

        _textExtractionServiceMock
            .Setup(s => s.ExtractTextAsync(fileStream, "application/pdf"))
            .ReturnsAsync(string.Empty);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("No text could be extracted", exception.Message);
    }

    [Fact]
    public async Task Given_DocumentWithNoExtractedClauses_When_HandleIsCalled_Then_InvalidOperationExceptionIsThrown()
    {
        var documentId = Guid.NewGuid();
        var document = Document.Create("contract.pdf", "application/pdf", "key/contract.pdf", 2048, "user-1");
        var command = new GenerateDocumentClausesCommand(documentId);
        var fileStream = new MemoryStream();
        var extractedText = "This is the full text of the legal contract.";

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId))
            .ReturnsAsync(document);

        _textExtractionServiceMock
            .Setup(s => s.SupportsContentType("application/pdf"))
            .Returns(true);

        _fileStorageServiceMock
            .Setup(s => s.DownloadFileAsync("key/contract.pdf"))
            .ReturnsAsync(fileStream);

        _textExtractionServiceMock
            .Setup(s => s.ExtractTextAsync(fileStream, "application/pdf"))
            .ReturnsAsync(extractedText);

        _clauseExtractorServiceMock
            .Setup(s => s.ExtractClausesAsync(extractedText, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClauseExtractionResult(Array.Empty<string>(), 1));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("No clauses could be extracted", exception.Message);
    }

    [Fact]
    public async Task Given_ValidDocument_When_HandleIsCalled_Then_ClausesAreReturnedWithoutPersistingDocument()
    {
        var documentId = Guid.NewGuid();
        var document = Document.Create("contract.pdf", "application/pdf", "key/contract.pdf", 2048, "user-1");
        var command = new GenerateDocumentClausesCommand(documentId);
        var fileStream = new MemoryStream();
        var extractedText = "This is the full text of the legal contract.";
        var clauses = new[]
        {
            "Clauza de confidentialitate",
            "Clauza de reziliere"
        };

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId))
            .ReturnsAsync(document);

        _textExtractionServiceMock
            .Setup(s => s.SupportsContentType("application/pdf"))
            .Returns(true);

        _fileStorageServiceMock
            .Setup(s => s.DownloadFileAsync("key/contract.pdf"))
            .ReturnsAsync(fileStream);

        _textExtractionServiceMock
            .Setup(s => s.ExtractTextAsync(fileStream, "application/pdf"))
            .ReturnsAsync(extractedText);

        _clauseExtractorServiceMock
            .Setup(s => s.ExtractClausesAsync(extractedText, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClauseExtractionResult(clauses, 2));

        var response = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(document.Id, response.DocumentId);
        Assert.Equal(2, response.ChunksProcessed);
        Assert.Equal(2, response.Clauses.Count);
        Assert.Contains("Clauza de confidentialitate", response.Clauses);
        _documentRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Document>()), Times.Never);
    }

    [Fact]
    public async Task Given_ValidDocument_When_HandleIsCalled_Then_TextIsExtractedAndClauseServiceIsCalled()
    {
        var documentId = Guid.NewGuid();
        var document = Document.Create("doc.pdf", "application/pdf", "key/doc.pdf", 1024, "user-1");
        var command = new GenerateDocumentClausesCommand(documentId);
        var fileStream = new MemoryStream();
        var extractedText = "Legal document content.";

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId))
            .ReturnsAsync(document);

        _textExtractionServiceMock
            .Setup(s => s.SupportsContentType("application/pdf"))
            .Returns(true);

        _fileStorageServiceMock
            .Setup(s => s.DownloadFileAsync("key/doc.pdf"))
            .ReturnsAsync(fileStream);

        _textExtractionServiceMock
            .Setup(s => s.ExtractTextAsync(fileStream, "application/pdf"))
            .ReturnsAsync(extractedText);

        _clauseExtractorServiceMock
            .Setup(s => s.ExtractClausesAsync(extractedText, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClauseExtractionResult(["Clause A"], 1));

        await _handler.Handle(command, CancellationToken.None);

        _fileStorageServiceMock.Verify(s => s.DownloadFileAsync("key/doc.pdf"), Times.Once);
        _textExtractionServiceMock.Verify(s => s.ExtractTextAsync(fileStream, "application/pdf"), Times.Once);
        _clauseExtractorServiceMock.Verify(s => s.ExtractClausesAsync(extractedText, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Given_ValidDocument_When_HandleIsCalled_Then_ResponseContainsGeneratedAtTimestamp()
    {
        var documentId = Guid.NewGuid();
        var document = Document.Create("file.pdf", "application/pdf", "key/file.pdf", 512, "user-1");
        var command = new GenerateDocumentClausesCommand(documentId);
        var fileStream = new MemoryStream();

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId))
            .ReturnsAsync(document);

        _textExtractionServiceMock
            .Setup(s => s.SupportsContentType("application/pdf"))
            .Returns(true);

        _fileStorageServiceMock
            .Setup(s => s.DownloadFileAsync("key/file.pdf"))
            .ReturnsAsync(fileStream);

        _textExtractionServiceMock
            .Setup(s => s.ExtractTextAsync(fileStream, "application/pdf"))
            .ReturnsAsync("Some text content");

        _clauseExtractorServiceMock
            .Setup(s => s.ExtractClausesAsync("Some text content", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClauseExtractionResult(["Clause A", "Clause B"], 2));

        var before = DateTime.UtcNow;

        var response = await _handler.Handle(command, CancellationToken.None);

        var after = DateTime.UtcNow;
        Assert.InRange(response.GeneratedAt, before, after);
    }
}

