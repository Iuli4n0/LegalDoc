using DocumentService.Application.Abstractions;
using DocumentService.Application.Queries.GetUserDocuments;
using DocumentService.Application.Queries.GetDocument;
using DocumentService.Domain.Entities;
using Moq;

namespace DocumentService.Test.Queries;

public class GetUserDocumentsQueryHandlerTests
{
    private readonly Mock<IDocumentRepository> _docRepo = new();
    private readonly GetUserDocumentsQueryHandler _handler;

    public GetUserDocumentsQueryHandlerTests()
    {
        _handler = new GetUserDocumentsQueryHandler(_docRepo.Object);
    }

    [Fact]
    public async Task Given_UserWithDocuments_When_Handle_Then_ReturnsDocuments()
    {
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "user-1");
        _docRepo.Setup(r => r.GetByUserIdAsync("user-1", 1, 10, "UploadedAt", false))
            .ReturnsAsync(new[] { doc });
        _docRepo.Setup(r => r.CountByUserIdAsync("user-1")).ReturnsAsync(1);

        var query = new GetUserDocumentsQuery("user-1", 1, 10, "UploadedAt", false);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Given_UserWithNoDocuments_When_Handle_Then_ReturnsEmpty()
    {
        _docRepo.Setup(r => r.GetByUserIdAsync("user-1", 1, 10, "UploadedAt", false))
            .ReturnsAsync(Enumerable.Empty<Document>());
        _docRepo.Setup(r => r.CountByUserIdAsync("user-1")).ReturnsAsync(0);

        var query = new GetUserDocumentsQuery("user-1", 1, 10, "UploadedAt", false);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }
}
