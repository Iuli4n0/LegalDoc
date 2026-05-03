using DocumentService.Application.Abstractions;
using DocumentService.Application.Queries.GetDocumentConversation;
using DocumentService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentService.Test.Queries;

public class GetDocumentConversationQueryHandlerTests
{
    private readonly Mock<IDocumentRepository> _docRepo = new();
    private readonly Mock<IDocumentMessageRepository> _msgRepo = new();
    private readonly GetDocumentConversationQueryHandler _handler;

    public GetDocumentConversationQueryHandlerTests()
    {
        _handler = new GetDocumentConversationQueryHandler(_docRepo.Object, _msgRepo.Object,
            new Mock<ILogger<GetDocumentConversationQueryHandler>>().Object);
    }

    [Fact]
    public async Task DocNotFound_Throws()
    {
        _docRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Document?)null);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new GetDocumentConversationQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task NoMessages_ReturnsEmpty()
    {
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        _docRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);
        _msgRepo.Setup(r => r.GetByDocumentIdAsync(It.IsAny<Guid>())).ReturnsAsync(new List<DocumentMessage>());

        var result = await _handler.Handle(new GetDocumentConversationQuery(doc.Id), CancellationToken.None);

        Assert.Empty(result.Messages);
    }

    [Fact]
    public async Task WithMessages_ReturnsMappedDtos()
    {
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        _docRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);

        var msg = DocumentMessage.Create(doc.Id, true, "Question");
        _msgRepo.Setup(r => r.GetByDocumentIdAsync(It.IsAny<Guid>())).ReturnsAsync(new List<DocumentMessage> { msg });

        var result = await _handler.Handle(new GetDocumentConversationQuery(doc.Id), CancellationToken.None);

        Assert.Single(result.Messages);
        Assert.True(result.Messages[0].IsUser);
        Assert.Equal("Question", result.Messages[0].Text);
    }
}
