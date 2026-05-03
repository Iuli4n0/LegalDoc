using DocumentService.Domain.Entities;
using DocumentService.Infrastructure.Persistence;
using DocumentService.Infrastructure.Repositories;
using DocumentService.Test.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentService.Test.Infrastructure.Repositories;

public class DocumentChunkRepositoryTests
{
    private static TestAppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestAppDbContext(options);
    }

    [Fact]
    public async Task AddRangeAsync_Persists()
    {
        await using var ctx = CreateContext();
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        var repo = new DocumentChunkRepository(ctx, new Mock<ILogger<DocumentChunkRepository>>().Object);
        var chunk = DocumentChunk.Create(doc.Id, 0, "text", new float[] { 0.1f });
        await repo.AddRangeAsync([chunk]);

        Assert.Equal(1, await ctx.DocumentChunks.CountAsync());
    }

    [Fact]
    public async Task IsDocumentIndexedAsync_True()
    {
        await using var ctx = CreateContext();
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        ctx.Documents.Add(doc);
        var chunk = DocumentChunk.Create(doc.Id, 0, "text", new float[] { 0.1f });
        ctx.DocumentChunks.Add(chunk);
        await ctx.SaveChangesAsync();

        var repo = new DocumentChunkRepository(ctx, new Mock<ILogger<DocumentChunkRepository>>().Object);
        Assert.True(await repo.IsDocumentIndexedAsync(doc.Id));
    }

    [Fact]
    public async Task IsDocumentIndexedAsync_False()
    {
        await using var ctx = CreateContext();
        var repo = new DocumentChunkRepository(ctx, new Mock<ILogger<DocumentChunkRepository>>().Object);
        Assert.False(await repo.IsDocumentIndexedAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteByDocumentIdAsync_Removes()
    {
        await using var ctx = CreateContext();
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        ctx.Documents.Add(doc);
        ctx.DocumentChunks.Add(DocumentChunk.Create(doc.Id, 0, "t", new float[] { 0.1f }));
        await ctx.SaveChangesAsync();

        var repo = new DocumentChunkRepository(ctx, new Mock<ILogger<DocumentChunkRepository>>().Object);
        await repo.DeleteByDocumentIdAsync(doc.Id);
        Assert.Equal(0, await ctx.DocumentChunks.CountAsync());
    }

    [Fact]
    public async Task DeleteByDocumentIdAsync_NoChunks_NoOp()
    {
        await using var ctx = CreateContext();
        var repo = new DocumentChunkRepository(ctx, new Mock<ILogger<DocumentChunkRepository>>().Object);
        await repo.DeleteByDocumentIdAsync(Guid.NewGuid());
        Assert.Equal(0, await ctx.DocumentChunks.CountAsync());
    }

    [Fact]
    public async Task CountByDocumentIdAsync_ReturnsCount()
    {
        await using var ctx = CreateContext();
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        ctx.Documents.Add(doc);
        ctx.DocumentChunks.Add(DocumentChunk.Create(doc.Id, 0, "t1", new float[] { 0.1f }));
        ctx.DocumentChunks.Add(DocumentChunk.Create(doc.Id, 1, "t2", new float[] { 0.2f }));
        await ctx.SaveChangesAsync();

        var repo = new DocumentChunkRepository(ctx, new Mock<ILogger<DocumentChunkRepository>>().Object);
        Assert.Equal(2, await repo.CountByDocumentIdAsync(doc.Id));
    }
}
