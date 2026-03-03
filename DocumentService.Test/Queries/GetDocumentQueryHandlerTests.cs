using DocumentService.Application.Abstractions;
using DocumentService.Application.Queries.GetDocument;
using DocumentService.Domain.Entities;
using Moq;

namespace DocumentService.Test.Queries;

public class GetDocumentQueryHandlerTests
{
    private readonly Mock<IDocumentRepository> _documentRepositoryMock;
    private readonly GetDocumentQueryHandler _handler;

    public GetDocumentQueryHandlerTests()
    {
        _documentRepositoryMock = new Mock<IDocumentRepository>();
        _handler = new GetDocumentQueryHandler(_documentRepositoryMock.Object);
    }

    [Fact]
    public async Task Given_ExistingDocumentId_When_HandleIsCalled_Then_DocumentResponseIsReturned()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = Document.Create("contract.pdf", "application/pdf", "key/contract.pdf", 2048, "user-123");
        var query = new GetDocumentQuery(documentId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId))
            .ReturnsAsync(document);

        // Act
        var response = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal(document.Id, response!.Id);
        Assert.Equal("user-123", response.UserId);
        Assert.Equal("contract.pdf", response.FileName);
        Assert.Equal("application/pdf", response.ContentType);
        Assert.Equal("key/contract.pdf", response.S3Key);
        Assert.Equal(2048, response.FileSize);
    }

    [Fact]
    public async Task Given_NonExistentDocumentId_When_HandleIsCalled_Then_NullIsReturned()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var query = new GetDocumentQuery(documentId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId))
            .ReturnsAsync((Document?)null);

        // Act
        var response = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(response);
    }

    [Fact]
    public async Task Given_DocumentWithResume_When_HandleIsCalled_Then_ResponseIncludesResumeData()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = Document.Create("report.pdf", "application/pdf", "key/report.pdf", 4096, "user-456");
        document.SetResume("This is the resume of the document.");
        var query = new GetDocumentQuery(documentId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId))
            .ReturnsAsync(document);

        // Act
        var response = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Equal("This is the resume of the document.", response!.Resume);
        Assert.NotNull(response.ResumeGeneratedAt);
    }

    [Fact]
    public async Task Given_DocumentWithoutResume_When_HandleIsCalled_Then_ResumeFieldsAreNull()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = Document.Create("file.pdf", "application/pdf", "key/file.pdf", 1024, "user-789");
        var query = new GetDocumentQuery(documentId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId))
            .ReturnsAsync(document);

        // Act
        var response = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.Null(response!.Resume);
        Assert.Null(response.ResumeGeneratedAt);
    }

    [Fact]
    public async Task Given_ValidDocumentId_When_HandleIsCalled_Then_RepositoryIsQueriedOnce()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var query = new GetDocumentQuery(documentId);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId))
            .ReturnsAsync((Document?)null);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _documentRepositoryMock.Verify(r => r.GetByIdAsync(documentId), Times.Once);
    }
}

