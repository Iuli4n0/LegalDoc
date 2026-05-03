using DocumentService.Application.Abstractions;
using DocumentService.Application.Commands.AddDocumentClause;
using DocumentService.Domain.Entities;
using Moq;

namespace DocumentService.Test.Commands;

public class AddDocumentClauseCommandHandlerTests
{
    private readonly Mock<IDocumentRepository> _documentRepoMock = new();
    private readonly Mock<IClauseRepository> _clauseRepoMock = new();
    private readonly AddDocumentClauseCommandHandler _handler;

    public AddDocumentClauseCommandHandlerTests()
    {
        _handler = new AddDocumentClauseCommandHandler(_documentRepoMock.Object, _clauseRepoMock.Object);
    }

    [Fact]
    public async Task Given_DocumentNotFound_When_HandleIsCalled_Then_ThrowsInvalidOperationException()
    {
        _documentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Document?)null);

        var command = new AddDocumentClauseCommand(Guid.NewGuid(), "user-1", "Clause text");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Given_WrongUserId_When_HandleIsCalled_Then_ThrowsInvalidOperationException()
    {
        var doc = Document.Create("file.pdf", "application/pdf", "key", 100, "other-user");
        _documentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);

        var command = new AddDocumentClauseCommand(doc.Id, "user-1", "Clause text");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Given_ValidRequest_When_HandleIsCalled_Then_ReturnsAddDocumentClauseResponse()
    {
        var doc = Document.Create("file.pdf", "application/pdf", "key", 100, "user-1");
        _documentRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);
        _clauseRepoMock.Setup(r => r.AddAsync(It.IsAny<Clause>())).Returns(Task.CompletedTask);

        var command = new AddDocumentClauseCommand(doc.Id, "user-1", "A valid clause text");

        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.ClauseId);
        Assert.Equal("A valid clause text", result.Text);
        Assert.Null(result.IsAbusive);
        Assert.Null(result.AbusiveProbability);
        Assert.Null(result.ClassifiedAt);
        _clauseRepoMock.Verify(r => r.AddAsync(It.IsAny<Clause>()), Times.Once);
    }
}
