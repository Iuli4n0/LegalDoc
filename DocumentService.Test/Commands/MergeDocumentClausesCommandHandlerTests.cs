using DocumentService.Application.Abstractions;
using DocumentService.Application.Commands.MergeDocumentClauses;
using DocumentService.Domain.Entities;
using Moq;

namespace DocumentService.Test.Commands;

public class MergeDocumentClausesCommandHandlerTests
{
    private readonly Mock<IDocumentRepository> _documentRepoMock = new();
    private readonly Mock<IClauseRepository> _clauseRepoMock = new();
    private readonly MergeDocumentClausesCommandHandler _handler;

    public MergeDocumentClausesCommandHandlerTests()
    {
        _handler = new MergeDocumentClausesCommandHandler(_documentRepoMock.Object, _clauseRepoMock.Object);
    }

    [Fact]
    public async Task Given_DocumentNotFound_When_HandleIsCalled_Then_ThrowsInvalidOperationException()
    {
        _documentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Document?)null);

        var command = new MergeDocumentClausesCommand(Guid.NewGuid(), "user-1", Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Given_WrongUserId_When_HandleIsCalled_Then_ThrowsInvalidOperationException()
    {
        var doc = Document.Create("file.pdf", "application/pdf", "key", 100, "other-user");
        _documentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);

        var command = new MergeDocumentClausesCommand(doc.Id, "user-1", Guid.NewGuid(), Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Given_FirstClauseNotFound_When_HandleIsCalled_Then_ThrowsInvalidOperationException()
    {
        var doc = Document.Create("file.pdf", "application/pdf", "key", 100, "user-1");
        _documentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);
        _clauseRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Clause?)null);

        var command = new MergeDocumentClausesCommand(doc.Id, "user-1", Guid.NewGuid(), Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("First clause not found", ex.Message);
    }

    [Fact]
    public async Task Given_FirstClauseBelongsToDifferentDocument_When_HandleIsCalled_Then_ThrowsInvalidOperationException()
    {
        var doc = Document.Create("file.pdf", "application/pdf", "key", 100, "user-1");
        _documentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);

        var clause1 = Clause.Create(Guid.NewGuid(), "Wrong doc clause");
        _clauseRepoMock
            .Setup(r => r.GetByIdAsync(clause1.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clause1);
        _clauseRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Clause?)null);

        var command = new MergeDocumentClausesCommand(doc.Id, "user-1", clause1.Id, Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("First clause not found", ex.Message);
    }

    [Fact]
    public async Task Given_SecondClauseNotFound_When_HandleIsCalled_Then_ThrowsInvalidOperationException()
    {
        var doc = Document.Create("file.pdf", "application/pdf", "key", 100, "user-1");
        _documentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);

        var clause1 = Clause.Create(doc.Id, "Clause one");
        _clauseRepoMock.SetupSequence(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clause1)
            .ReturnsAsync((Clause?)null);

        var command = new MergeDocumentClausesCommand(doc.Id, "user-1", clause1.Id, Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Second clause not found", ex.Message);
    }

    [Fact]
    public async Task Given_SecondClauseBelongsToDifferentDocument_When_HandleIsCalled_Then_ThrowsInvalidOperationException()
    {
        var doc = Document.Create("file.pdf", "application/pdf", "key", 100, "user-1");
        _documentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);

        var clause1 = Clause.Create(doc.Id, "Clause one");
        var clause2 = Clause.Create(Guid.NewGuid(), "Wrong doc clause");

        _clauseRepoMock.SetupSequence(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clause1)
            .ReturnsAsync(clause2);

        var command = new MergeDocumentClausesCommand(doc.Id, "user-1", clause1.Id, clause2.Id);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
        Assert.Contains("Second clause not found", ex.Message);
    }

    [Fact]
    public async Task Given_ValidRequest_When_HandleIsCalled_Then_ClausesAreMergedAndReturned()
    {
        var doc = Document.Create("file.pdf", "application/pdf", "key", 100, "user-1");
        _documentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);

        var clause1 = Clause.Create(doc.Id, "First clause text");
        var clause2 = Clause.Create(doc.Id, "Second clause text");

        _clauseRepoMock.SetupSequence(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(clause1)
            .ReturnsAsync(clause2);

        _clauseRepoMock.Setup(r => r.AddAsync(It.IsAny<Clause>())).Returns(Task.CompletedTask);
        _clauseRepoMock.Setup(r => r.DeleteAsync(It.IsAny<Clause>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var command = new MergeDocumentClausesCommand(doc.Id, "user-1", clause1.Id, clause2.Id);

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.ClauseId);
        Assert.Contains("First clause text", result.Text);
        Assert.Contains("Second clause text", result.Text);
        Assert.Null(result.IsAbusive);
        _clauseRepoMock.Verify(r => r.AddAsync(It.IsAny<Clause>()), Times.Once);
        _clauseRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Clause>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
