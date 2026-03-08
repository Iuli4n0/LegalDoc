using System;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using DocumentService.Application.Commands.DeleteDocument;
using DocumentService.Domain.Entities;
using Moq;
using Xunit;

namespace DocumentService.Test.Commands;

public class DeleteDocumentCommandHandlerTests
{
    private readonly Mock<IDocumentRepository> _documentRepositoryMock = new();
    private readonly Mock<IFileStorageService> _fileStorageServiceMock = new();
    private readonly DeleteDocumentCommandHandler _handler;

    public DeleteDocumentCommandHandlerTests()
    {
        _handler = new DeleteDocumentCommandHandler(
            _documentRepositoryMock.Object,
            _fileStorageServiceMock.Object);
    }

    private static Document CreateDocument(string userId = "user-123", string s3Key = "keys/doc.pdf")
    {
        return Document.Create("doc.pdf", "application/pdf", s3Key, 1024, userId);
    }

    [Fact]
    public async Task Given_ValidCommand_When_HandleIsCalled_Then_DocumentIsDeletedFromRepositoryAndS3()
    {
        // Arrange
        var document = CreateDocument();
        var command = new DeleteDocumentCommand(document.Id, "user-123");

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(document.Id))
            .ReturnsAsync(document);

        _documentRepositoryMock
            .Setup(r => r.DeleteAsync(document))
            .Returns(Task.CompletedTask);

        _fileStorageServiceMock
            .Setup(s => s.DeleteFileAsync(document.S3Key))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _documentRepositoryMock.Verify(r => r.DeleteAsync(document), Times.Once);
        _fileStorageServiceMock.Verify(s => s.DeleteFileAsync(document.S3Key), Times.Once);
    }

    [Fact]
    public async Task Given_DocumentDoesNotExist_When_HandleIsCalled_Then_KeyNotFoundExceptionIsThrown()
    {
        // Arrange
        var id = Guid.NewGuid();
        var command = new DeleteDocumentCommand(id, "user-123");

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync((Document?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _documentRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Document>()), Times.Never);
        _fileStorageServiceMock.Verify(s => s.DeleteFileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Given_DocumentOwnedByDifferentUser_When_HandleIsCalled_Then_UnauthorizedAccessExceptionIsThrown()
    {
        // Arrange
        var document = CreateDocument(userId: "owner-456");
        var command = new DeleteDocumentCommand(document.Id, "attacker-789");

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(document.Id))
            .ReturnsAsync(document);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _documentRepositoryMock.Verify(r => r.DeleteAsync(It.IsAny<Document>()), Times.Never);
        _fileStorageServiceMock.Verify(s => s.DeleteFileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Given_RepositoryDeleteFails_When_HandleIsCalled_Then_ExceptionIsPropagatedAndS3IsNotCalled()
    {
        // Arrange
        var document = CreateDocument();
        var command = new DeleteDocumentCommand(document.Id, "user-123");

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(document.Id))
            .ReturnsAsync(document);

        _documentRepositoryMock
            .Setup(r => r.DeleteAsync(document))
            .ThrowsAsync(new Exception("DB error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _handler.Handle(command, CancellationToken.None));

        _fileStorageServiceMock.Verify(s => s.DeleteFileAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Given_DbDeleteSucceeds_But_S3DeleteFails_When_HandleIsCalled_Then_ExceptionIsPropagated()
    {
        // Arrange
        var document = CreateDocument();
        var command = new DeleteDocumentCommand(document.Id, "user-123");

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(document.Id))
            .ReturnsAsync(document);

        _documentRepositoryMock
            .Setup(r => r.DeleteAsync(document))
            .Returns(Task.CompletedTask);

        _fileStorageServiceMock
            .Setup(s => s.DeleteFileAsync(document.S3Key))
            .ThrowsAsync(new InvalidOperationException("S3 error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _documentRepositoryMock.Verify(r => r.DeleteAsync(document), Times.Once);
    }
}

