using DocumentService.Application.Abstractions;
using DocumentService.Application.Commands.AskDocumentQuestion;
using DocumentService.Application.Commands.IndexDocument;
using DocumentService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentService.Test.Commands;

public class AskDocumentQuestionCommandHandlerTests
{
    private readonly Mock<IDocumentRepository> _docRepo = new();
    private readonly Mock<IDocumentChunkRepository> _chunkRepo = new();
    private readonly Mock<IDocumentMessageRepository> _msgRepo = new();
    private readonly Mock<IEmbeddingService> _embedding = new();
    private readonly Mock<IQAService> _qa = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly AskDocumentQuestionCommandHandler _handler;

    public AskDocumentQuestionCommandHandlerTests()
    {
        _handler = new AskDocumentQuestionCommandHandler(
            _docRepo.Object, _chunkRepo.Object, _msgRepo.Object,
            _embedding.Object, _qa.Object, _mediator.Object,
            new Mock<ILogger<AskDocumentQuestionCommandHandler>>().Object);
        _msgRepo.Setup(r => r.AddAsync(It.IsAny<DocumentMessage>())).Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task DocNotFound_Throws()
    {
        _docRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Document?)null);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new AskDocumentQuestionCommand(Guid.NewGuid(), "Q?"), CancellationToken.None));
    }

    [Fact]
    public async Task NotIndexed_IndexesFirst()
    {
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        _docRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);
        _chunkRepo.Setup(r => r.IsDocumentIndexedAsync(It.IsAny<Guid>())).ReturnsAsync(false);
        _mediator.Setup(m => m.Send(It.IsAny<IndexDocumentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexDocumentResponse(doc.Id, 1, DateTime.UtcNow));
        _embedding.Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f });
        _chunkRepo.Setup(r => r.SearchSimilarAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>()))
            .ReturnsAsync(new List<DocumentChunkSearchResult> { new(Guid.NewGuid(), 0, "chunk", 0.5) });
        _qa.Setup(s => s.GenerateAnswerAsync(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");

        var result = await _handler.Handle(new AskDocumentQuestionCommand(doc.Id, "Q?"), CancellationToken.None);

        Assert.True(result.IsNewlyIndexed);
        Assert.Equal("Answer", result.Answer);
        _mediator.Verify(m => m.Send(It.IsAny<IndexDocumentCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AlreadyIndexed_NoReindex()
    {
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        _docRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);
        _chunkRepo.Setup(r => r.IsDocumentIndexedAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        _embedding.Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f });
        _chunkRepo.Setup(r => r.SearchSimilarAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>()))
            .ReturnsAsync(new List<DocumentChunkSearchResult> { new(Guid.NewGuid(), 0, "chunk", 0.5) });
        _qa.Setup(s => s.GenerateAnswerAsync(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");

        var result = await _handler.Handle(new AskDocumentQuestionCommand(doc.Id, "Q?"), CancellationToken.None);

        Assert.False(result.IsNewlyIndexed);
        Assert.Equal("Answer", result.Answer);
    }

    [Fact]
    public async Task NoChunksFound_ReturnsNoRelevantChunksMessage()
    {
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        _docRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);
        _chunkRepo.Setup(r => r.IsDocumentIndexedAsync(It.IsAny<Guid>())).ReturnsAsync(true);
        _embedding.Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f });
        _chunkRepo.Setup(r => r.SearchSimilarAsync(It.IsAny<Guid>(), It.IsAny<float[]>(), It.IsAny<int>()))
            .ReturnsAsync(new List<DocumentChunkSearchResult>());

        var result = await _handler.Handle(new AskDocumentQuestionCommand(doc.Id, "Q?"), CancellationToken.None);

        Assert.Contains("Nu am găsit fragmente relevante", result.Answer);
        Assert.Empty(result.SourceChunks);
    }
}
