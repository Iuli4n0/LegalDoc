using DocumentService.Application.Abstractions;
using DocumentService.Application.Commands.GenerateDocumentResume;
using DocumentService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentService.Test.Commands;

public class GenerateDocumentResumeCommandHandlerTests
{
    private readonly Mock<IDocumentRepository> _documentRepositoryMock;
    private readonly Mock<IFileStorageService> _fileStorageServiceMock;
    private readonly Mock<ITextExtractionService> _textExtractionServiceMock;
    private readonly Mock<IResumeGeneratorService> _resumeGeneratorServiceMock;
    private readonly Mock<ILogger<GenerateDocumentResumeCommandHandler>> _loggerMock;
    private readonly GenerateDocumentResumeCommandHandler _handler;

    public GenerateDocumentResumeCommandHandlerTests()
    {
        _documentRepositoryMock = new Mock<IDocumentRepository>();
        _fileStorageServiceMock = new Mock<IFileStorageService>();
        _textExtractionServiceMock = new Mock<ITextExtractionService>();
        _resumeGeneratorServiceMock = new Mock<IResumeGeneratorService>();
        _loggerMock = new Mock<ILogger<GenerateDocumentResumeCommandHandler>>();

        _handler = new GenerateDocumentResumeCommandHandler(
            _documentRepositoryMock.Object,
            _fileStorageServiceMock.Object,
            _textExtractionServiceMock.Object,
            _resumeGeneratorServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Given_NonExistentDocumentId_When_HandleIsCalled_Then_InvalidOperationExceptionIsThrown()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var command = new GenerateDocumentResumeCommand(documentId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId))
            .ReturnsAsync((Document?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains(documentId.ToString(), exception.Message);
        Assert.Contains("not found", exception.Message);
    }

    [Fact]
    public async Task Given_DocumentWithUnsupportedContentType_When_HandleIsCalled_Then_NotSupportedExceptionIsThrown()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = Document.Create("image.png", "image/png", "key/image.png", 5000, "user-1");
        var command = new GenerateDocumentResumeCommand(documentId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId))
            .ReturnsAsync(document);

        _textExtractionServiceMock
            .Setup(s => s.SupportsContentType("image/png"))
            .Returns(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotSupportedException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("image/png", exception.Message);
    }

    [Fact]
    public async Task Given_DocumentWithEmptyExtractedText_When_HandleIsCalled_Then_InvalidOperationExceptionIsThrown()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = Document.Create("empty.pdf", "application/pdf", "key/empty.pdf", 100, "user-1");
        var command = new GenerateDocumentResumeCommand(documentId);
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

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("No text could be extracted", exception.Message);
    }

    [Fact]
    public async Task Given_ValidDocument_When_HandleIsCalled_Then_ResumeIsGeneratedAndSaved()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = Document.Create("contract.pdf", "application/pdf", "key/contract.pdf", 2048, "user-1");
        var command = new GenerateDocumentResumeCommand(documentId);
        var fileStream = new MemoryStream();
        var extractedText = "This is the full text of the legal contract.";
        var resumeResult = new ResumeResult("This is a summary of the contract.", 1);

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

        _resumeGeneratorServiceMock
            .Setup(s => s.GenerateResumeAsync(extractedText, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resumeResult);

        _documentRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Document>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(document.Id, response.DocumentId);
        Assert.Equal("This is a summary of the contract.", response.Resume);
        Assert.Equal(1, response.ChunksProcessed);
        _documentRepositoryMock.Verify(r => r.UpdateAsync(document), Times.Once);
    }

    [Fact]
    public async Task Given_ValidDocument_When_HandleIsCalled_Then_TextIsExtractedFromDownloadedFile()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = Document.Create("doc.pdf", "application/pdf", "key/doc.pdf", 1024, "user-1");
        var command = new GenerateDocumentResumeCommand(documentId);
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

        _resumeGeneratorServiceMock
            .Setup(s => s.GenerateResumeAsync(extractedText, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResumeResult("Summary", 1));

        _documentRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Document>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _fileStorageServiceMock.Verify(s => s.DownloadFileAsync("key/doc.pdf"), Times.Once);
        _textExtractionServiceMock.Verify(s => s.ExtractTextAsync(fileStream, "application/pdf"), Times.Once);
        _resumeGeneratorServiceMock.Verify(s => s.GenerateResumeAsync(extractedText, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Given_ValidDocument_When_HandleIsCalled_Then_ResponseContainsGeneratedAtTimestamp()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = Document.Create("file.pdf", "application/pdf", "key/file.pdf", 512, "user-1");
        var command = new GenerateDocumentResumeCommand(documentId);
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

        _resumeGeneratorServiceMock
            .Setup(s => s.GenerateResumeAsync("Some text content", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResumeResult("Resume text", 2));

        _documentRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Document>()))
            .Returns(Task.CompletedTask);

        var before = DateTime.UtcNow;

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var after = DateTime.UtcNow;
        Assert.InRange(response.GeneratedAt, before, after);
    }

    [Fact]
    public async Task Given_WhitespaceOnlyExtractedText_When_HandleIsCalled_Then_InvalidOperationExceptionIsThrown()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = Document.Create("blank.pdf", "application/pdf", "key/blank.pdf", 100, "user-1");
        var command = new GenerateDocumentResumeCommand(documentId);
        var fileStream = new MemoryStream();

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId))
            .ReturnsAsync(document);

        _textExtractionServiceMock
            .Setup(s => s.SupportsContentType("application/pdf"))
            .Returns(true);

        _fileStorageServiceMock
            .Setup(s => s.DownloadFileAsync("key/blank.pdf"))
            .ReturnsAsync(fileStream);

        _textExtractionServiceMock
            .Setup(s => s.ExtractTextAsync(fileStream, "application/pdf"))
            .ReturnsAsync("   \t\n  ");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("No text could be extracted", exception.Message);
    }
}

