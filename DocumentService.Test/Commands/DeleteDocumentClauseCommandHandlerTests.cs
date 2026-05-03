using DocumentService.Application.Abstractions;
using DocumentService.Application.Commands.DeleteDocumentClause;
using DocumentService.Domain.Entities;
using Moq;

namespace DocumentService.Test.Commands;

public class DeleteDocumentClauseCommandHandlerTests
{
    private readonly Mock<IDocumentRepository> _documentRepoMock = new();
    private readonly Mock<IClauseRepository> _clauseRepoMock = new();
    private readonly DeleteDocumentClauseCommandHandler _handler;

    public DeleteDocumentClauseCommandHandlerTests()
    {
        _handler = new DeleteDocumentClauseCommandHandler(_documentRepoMock.Object, _clauseRepoMock.Object);
    }

    [Fact]
    public async Task Given_DocumentNotFound_When_HandleIsCalled_Then_ThrowsInvalidOperationException()
    {
        _documentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Document?)null);

        var command = new DeleteDocumentClauseCommand(Guid.NewGuid(), "user-1", Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Given_WrongUserId_When_HandleIsCalled_Then_ThrowsInvalidOperationException()
    {
        var doc = Document.Create("file.pdf", "application/pdf", "key", 100, "other-user");
        _documentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);

        var command = new DeleteDocumentClauseCommand(doc.Id, "user-1", Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Given_ClauseNotFound_When_HandleIsCalled_Then_ThrowsInvalidOperationException()
    {
        var doc = Document.Create("file.pdf", "application/pdf", "key", 100, "user-1");
        _documentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);
        _clauseRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Clause?)null);

        var command = new DeleteDocumentClauseCommand(doc.Id, "user-1", Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Given_ClauseBelongsToDifferentDocument_When_HandleIsCalled_Then_ThrowsInvalidOperationException()
    {
        var doc = Document.Create("file.pdf", "application/pdf", "key", 100, "user-1");
        _documentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);

        var clause = Clause.Create(Guid.NewGuid(), "Different doc clause"); // different document ID
        _clauseRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(clause);

        var command = new DeleteDocumentClauseCommand(doc.Id, "user-1", clause.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Given_ValidRequest_When_HandleIsCalled_Then_ClauseIsDeleted()
    {
        var doc = Document.Create("file.pdf", "application/pdf", "key", 100, "user-1");
        _documentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);

        var clause = Clause.Create(doc.Id, "Clause to delete");
        _clauseRepoMock.Setup(r => r.GetByIdAsync(clause.Id, It.IsAny<CancellationToken>())).ReturnsAsync(clause);
        _clauseRepoMock.Setup(r => r.DeleteAsync(clause, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var command = new DeleteDocumentClauseCommand(doc.Id, "user-1", clause.Id);

        await _handler.Handle(command, CancellationToken.None);

        _clauseRepoMock.Verify(r => r.DeleteAsync(clause, It.IsAny<CancellationToken>()), Times.Once);
    }
}
