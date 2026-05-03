using DocumentService.Domain.Entities;
using Pgvector;

namespace DocumentService.Test.Domain;

public class DocumentChunkTests
{
    [Fact]
    public void Given_ValidParameters_When_CreateIsCalled_Then_ChunkIsCreatedCorrectly()
    {
        var documentId = Guid.NewGuid();
        var text = "This is some chunk text.";
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };

        var chunk = DocumentChunk.Create(documentId, 0, text, embedding);

        Assert.NotEqual(Guid.Empty, chunk.Id);
        Assert.Equal(documentId, chunk.DocumentId);
        Assert.Equal(0, chunk.ChunkIndex);
        Assert.Equal(text, chunk.Text);
        Assert.NotNull(chunk.Embedding);
        Assert.True(chunk.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Given_TwoChunksCreated_When_Compared_Then_UniqueIdsAreGenerated()
    {
        var docId = Guid.NewGuid();
        var embedding = new float[] { 0.1f };
        var chunk1 = DocumentChunk.Create(docId, 0, "Text A", embedding);
        var chunk2 = DocumentChunk.Create(docId, 1, "Text B", embedding);

        Assert.NotEqual(chunk1.Id, chunk2.Id);
    }

    [Fact]
    public void Given_EmptyDocumentId_When_CreateIsCalled_Then_ArgumentExceptionIsThrown()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            DocumentChunk.Create(Guid.Empty, 0, "Text", new float[] { 0.1f }));

        Assert.Equal("documentId", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_InvalidText_When_CreateIsCalled_Then_ArgumentExceptionIsThrown(string? text)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            DocumentChunk.Create(Guid.NewGuid(), 0, text!, new float[] { 0.1f }));

        Assert.Equal("text", exception.ParamName);
    }

    [Fact]
    public void Given_NullEmbedding_When_CreateIsCalled_Then_ArgumentExceptionIsThrown()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            DocumentChunk.Create(Guid.NewGuid(), 0, "Text", null!));

        Assert.Equal("embedding", exception.ParamName);
    }

    [Fact]
    public void Given_EmptyEmbedding_When_CreateIsCalled_Then_ArgumentExceptionIsThrown()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            DocumentChunk.Create(Guid.NewGuid(), 0, "Text", Array.Empty<float>()));

        Assert.Equal("embedding", exception.ParamName);
    }
}
