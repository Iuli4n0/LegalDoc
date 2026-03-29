using DocumentService.Application.Abstractions;
using DocumentService.Application.Commands.ClassifyDocumentClauses;
using DocumentService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentService.Test.Commands;

public class ClassifyDocumentClausesCommandHandlerTests
{
    private readonly Mock<IDocumentRepository> _documentRepositoryMock = new();
    private readonly Mock<IClauseRepository> _clauseRepositoryMock = new();
    private readonly Mock<IClauseClassificationService> _classificationServiceMock = new();
    private readonly Mock<ILogger<ClassifyDocumentClausesCommandHandler>> _loggerMock = new();

    [Fact]
    public async Task Given_MissingDocument_When_HandleIsCalled_Then_InvalidOperationExceptionIsThrown()
    {
        var handler = CreateHandler();
        var id = Guid.NewGuid();

        _documentRepositoryMock
            .Setup(r => r.GetByIdAsync(id))
            .ReturnsAsync((Document?)null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new ClassifyDocumentClausesCommand(id), CancellationToken.None));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task Given_DocumentWithoutClauses_When_HandleIsCalled_Then_InvalidOperationExceptionIsThrown()
    {
        var handler = CreateHandler();
        var id = Guid.NewGuid();
        var document = Document.Create("a.pdf", "application/pdf", "k", 1, "user-1");

        _documentRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(document);
        _clauseRepositoryMock.Setup(r => r.GetByDocumentIdAsync(id)).ReturnsAsync(Array.Empty<Clause>());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new ClassifyDocumentClausesCommand(id), CancellationToken.None));

        Assert.Contains("No clauses", ex.Message);
    }

    [Fact]
    public async Task Given_ValidDocumentClauses_When_HandleIsCalled_Then_AllClausesAreClassifiedAndPersisted()
    {
        var handler = CreateHandler();
        var id = Guid.NewGuid();
        var document = Document.Create("a.pdf", "application/pdf", "k", 1, "user-1");
        var clauseA = Clause.Create(id, "Clauza A");
        var clauseB = Clause.Create(id, "Clauza B");

        _documentRepositoryMock.Setup(r => r.GetByIdAsync(id)).ReturnsAsync(document);
        _clauseRepositoryMock.Setup(r => r.GetByDocumentIdAsync(id)).ReturnsAsync([clauseA, clauseB]);
        _classificationServiceMock.Setup(s => s.ClassifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClauseClassificationResult(1, 0.79));

        var response = await handler.Handle(new ClassifyDocumentClausesCommand(id), CancellationToken.None);

        Assert.Equal(2, response.Clauses.Count);
        Assert.All(response.Clauses, c => Assert.True(c.IsAbusive));
        _clauseRepositoryMock.Verify(r => r.UpdateRangeAsync(It.Is<IReadOnlyList<Clause>>(x => x.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
    }

    private ClassifyDocumentClausesCommandHandler CreateHandler()
    {
        return new ClassifyDocumentClausesCommandHandler(
            _documentRepositoryMock.Object,
            _clauseRepositoryMock.Object,
            _classificationServiceMock.Object,
            _loggerMock.Object);
    }
}

