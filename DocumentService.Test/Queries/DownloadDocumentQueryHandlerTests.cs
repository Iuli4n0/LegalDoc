using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using DocumentService.Application.Queries.DownloadDocument;
using DocumentService.Domain.Entities;
using Moq;

namespace DocumentService.Test.Queries;

public class DownloadDocumentQueryHandlerTests
{
    private readonly Mock<IDocumentRepository> _documentRepositoryMock = new();
    private readonly Mock<IFileStorageService> _fileStorageServiceMock = new();
    private readonly DownloadDocumentQueryHandler _handler;

    public DownloadDocumentQueryHandlerTests()
    {
        _handler = new DownloadDocumentQueryHandler(
            _documentRepositoryMock.Object,
            _fileStorageServiceMock.Object);
    }

    [Fact]
    public async Task Given_ValidRequest_When_Handle_Then_DownloadResultIsReturned()
    {
        var documentId = Guid.NewGuid();
        var userId = "user-123";
        var document = Document.Create("contract.pdf", "application/pdf", "key/contract.pdf", 1024, userId);
        var fileStream = new MemoryStream([1, 2, 3]);

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId))
            .ReturnsAsync(document);

        _fileStorageServiceMock
            .Setup(s => s.DownloadFileAsync("key/contract.pdf"))
            .ReturnsAsync(fileStream);

        var query = new DownloadDocumentQuery(documentId, userId);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("contract.pdf", result.FileName);
        Assert.Equal("application/pdf", result.ContentType);
        Assert.Same(fileStream, result.Stream);
    }

    [Fact]
    public async Task Given_NonExistentDocument_When_Handle_Then_KeyNotFoundExceptionIsThrown()
    {
        var documentId = Guid.NewGuid();

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId))
            .ReturnsAsync((Document?)null);

        var query = new DownloadDocumentQuery(documentId, "user-123");

        var exception = await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _handler.Handle(query, CancellationToken.None));

        Assert.Contains(documentId.ToString(), exception.Message);
    }

    [Fact]
    public async Task Given_DocumentOwnedByAnotherUser_When_Handle_Then_UnauthorizedAccessExceptionIsThrown()
    {
        var documentId = Guid.NewGuid();
        var document = Document.Create("contract.pdf", "application/pdf", "key/contract.pdf", 1024, "owner-user");

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(documentId))
            .ReturnsAsync(document);

        var query = new DownloadDocumentQuery(documentId, "other-user");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(query, CancellationToken.None));
    }
}

