using DocumentService.Application.Abstractions;
using DocumentService.Application.Queries.GetDocumentClauses;
using DocumentService.Domain.Entities;
using Moq;

namespace DocumentService.Test.Queries;

public class GetDocumentClausesQueryHandlerTests
{
    private readonly Mock<IClauseRepository> _clauseRepo = new();
    private readonly Mock<IDocumentRepository> _docRepo = new();
    private readonly GetDocumentClausesQueryHandler _handler;

    public GetDocumentClausesQueryHandlerTests()
    {
        _handler = new GetDocumentClausesQueryHandler(_clauseRepo.Object, _docRepo.Object);
    }

    [Fact]
    public async Task DocNotFound_Throws()
    {
        _docRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Document?)null);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new GetDocumentClausesQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task NoClauses_ReturnsEmptyWithNullDate()
    {
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        _docRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);
        _clauseRepo.Setup(r => r.GetByDocumentIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<Clause>());

        var result = await _handler.Handle(new GetDocumentClausesQuery(doc.Id), CancellationToken.None);

        Assert.Empty(result.Clauses);
        Assert.Null(result.GeneratedAt);
    }

    [Fact]
    public async Task WithClauses_ReturnsClausesAndDate()
    {
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        _docRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);

        var clause = Clause.Create(doc.Id, "A clause");
        _clauseRepo.Setup(r => r.GetByDocumentIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<Clause> { clause });

        var result = await _handler.Handle(new GetDocumentClausesQuery(doc.Id), CancellationToken.None);

        Assert.Single(result.Clauses);
        Assert.Equal("A clause", result.Clauses[0].Text);
        Assert.NotNull(result.GeneratedAt);
    }
}
