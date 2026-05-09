using DocumentService.Application.Abstractions;
using DocumentService.Application.Commands.IndexDocument;
using DocumentService.Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentService.Test.Commands;

public class IndexDocumentCommandHandlerTests
{
    private static readonly float[] ChunkEmbedding = [0.1f, 0.2f, 0.3f];
    private readonly Mock<IDocumentRepository> _docRepo = new();
    private readonly Mock<IFileStorageService> _fileStorage = new();
    private readonly Mock<ITextExtractionService> _textExtraction = new();
    private readonly Mock<IEmbeddingService> _embedding = new();
    private readonly Mock<IDocumentChunkRepository> _chunkRepo = new();
    private readonly IndexDocumentCommandHandler _handler;

    public IndexDocumentCommandHandlerTests()
    {
        _handler = new IndexDocumentCommandHandler(_docRepo.Object, _fileStorage.Object,
            _textExtraction.Object, _embedding.Object, _chunkRepo.Object,
            new Mock<ILogger<IndexDocumentCommandHandler>>().Object);
    }

    [Fact]
    public async Task DocNotFound_Throws()
    {
        _docRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Document?)null);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new IndexDocumentCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task UnsupportedContentType_Throws()
    {
        var doc = Document.Create("f.xyz", "application/xyz", "k", 100, "u");
        _docRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);
        _textExtraction.Setup(s => s.SupportsContentType("application/xyz")).Returns(false);
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            _handler.Handle(new IndexDocumentCommand(doc.Id), CancellationToken.None));
    }

    [Fact]
    public async Task EmptyText_Throws()
    {
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        _docRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);
        _textExtraction.Setup(s => s.SupportsContentType("application/pdf")).Returns(true);
        _chunkRepo.Setup(r => r.DeleteByDocumentIdAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        _fileStorage.Setup(s => s.DownloadFileAsync(It.IsAny<string>())).ReturnsAsync(new MemoryStream());
        _textExtraction.Setup(s => s.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("   ");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new IndexDocumentCommand(doc.Id), CancellationToken.None));
    }

    [Fact]
    public async Task ValidDoc_CreatesChunks()
    {
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        _docRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(doc);
        _textExtraction.Setup(s => s.SupportsContentType("application/pdf")).Returns(true);
        _chunkRepo.Setup(r => r.DeleteByDocumentIdAsync(It.IsAny<Guid>())).Returns(Task.CompletedTask);
        _fileStorage.Setup(s => s.DownloadFileAsync(It.IsAny<string>())).ReturnsAsync(new MemoryStream());
        _textExtraction.Setup(s => s.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<string>())).ReturnsAsync("Short text.");
        _embedding.Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChunkEmbedding);
        _chunkRepo.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<DocumentChunk>>())).Returns(Task.CompletedTask);

        var result = await _handler.Handle(new IndexDocumentCommand(doc.Id), CancellationToken.None);

        Assert.Equal(doc.Id, result.DocumentId);
        Assert.True(result.ChunksCreated > 0);
    }


}
