using DocumentService.Application.Abstractions;
using DocumentService.Application.Commands.UploadDocument;
using DocumentService.Domain.Entities;
using Moq;

namespace DocumentService.Test.Commands;

public class UploadDocumentCommandHandlerTests
{
    private readonly Mock<IFileStorageService> _fileStorageServiceMock;
    private readonly Mock<IDocumentRepository> _documentRepositoryMock;
    private readonly UploadDocumentCommandHandler _handler;

    public UploadDocumentCommandHandlerTests()
    {
        _fileStorageServiceMock = new Mock<IFileStorageService>();
        _documentRepositoryMock = new Mock<IDocumentRepository>();
        _handler = new UploadDocumentCommandHandler(
            _fileStorageServiceMock.Object,
            _documentRepositoryMock.Object);
    }

    [Fact]
    public async Task Given_ValidCommand_When_HandleIsCalled_Then_FileIsUploadedToS3()
    {
        // Arrange
        var stream = new MemoryStream();
        var command = new UploadDocumentCommand
        {
            FileStream = stream,
            FileName = "contract.pdf",
            ContentType = "application/pdf",
            FileSize = 2048,
            UserId = "user-123"
        };

        _fileStorageServiceMock
            .Setup(s => s.UploadFileAsync(stream, "contract.pdf", "application/pdf"))
            .ReturnsAsync("documents/contract.pdf");

        _documentRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Document>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _fileStorageServiceMock.Verify(
            s => s.UploadFileAsync(stream, "contract.pdf", "application/pdf"),
            Times.Once);
    }

    [Fact]
    public async Task Given_ValidCommand_When_HandleIsCalled_Then_DocumentIsSavedToRepository()
    {
        // Arrange
        var stream = new MemoryStream();
        var command = new UploadDocumentCommand
        {
            FileStream = stream,
            FileName = "contract.pdf",
            ContentType = "application/pdf",
            FileSize = 2048,
            UserId = "user-123"
        };

        _fileStorageServiceMock
            .Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("documents/contract.pdf");

        _documentRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Document>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _documentRepositoryMock.Verify(
            r => r.AddAsync(It.Is<Document>(d =>
                d.FileName == "contract.pdf" &&
                d.ContentType == "application/pdf" &&
                d.S3Key == "documents/contract.pdf" &&
                d.FileSize == 2048 &&
                d.UserId == "user-123")),
            Times.Once);
    }

    [Fact]
    public async Task Given_ValidCommand_When_HandleIsCalled_Then_ResponseContainsCorrectData()
    {
        // Arrange
        var stream = new MemoryStream();
        var command = new UploadDocumentCommand
        {
            FileStream = stream,
            FileName = "document.docx",
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileSize = 4096,
            UserId = "user-456"
        };

        _fileStorageServiceMock
            .Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("documents/document.docx");

        _documentRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Document>()))
            .Returns(Task.CompletedTask);

        // Act
        var response = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, response.Id);
        Assert.Equal("user-456", response.UserId);
        Assert.Equal("document.docx", response.FileName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", response.ContentType);
        Assert.Equal("documents/document.docx", response.S3Key);
        Assert.Equal(4096, response.FileSize);
        Assert.True(response.UploadedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task Given_FileStorageThrowsException_When_HandleIsCalled_Then_ExceptionIsPropagated()
    {
        // Arrange
        var stream = new MemoryStream();
        var command = new UploadDocumentCommand
        {
            FileStream = stream,
            FileName = "file.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            UserId = "user-789"
        };

        _fileStorageServiceMock
            .Setup(s => s.UploadFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("S3 upload failed"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _handler.Handle(command, CancellationToken.None));

        _documentRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<Document>()),
            Times.Never);
    }
}

