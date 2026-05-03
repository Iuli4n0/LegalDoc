using DocumentService.Domain.Entities;
using DocumentService.Infrastructure.Persistence;
using DocumentService.Infrastructure.Repositories;
using DocumentService.Test.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocumentService.Test.Infrastructure.Repositories;

public class DocumentMessageRepositoryTests
{
    private static TestAppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestAppDbContext(options);
    }

    [Fact]
    public async Task AddAsync_Persists()
    {
        await using var ctx = CreateContext();
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        var repo = new DocumentMessageRepository(ctx);
        var msg = DocumentMessage.Create(doc.Id, true, "Q?");
        await repo.AddAsync(msg);

        Assert.NotNull(await ctx.DocumentMessages.FindAsync(msg.Id));
    }

    [Fact]
    public async Task AddRangeAsync_PersistsMultiple()
    {
        await using var ctx = CreateContext();
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        var repo = new DocumentMessageRepository(ctx);
        await repo.AddRangeAsync([
            DocumentMessage.Create(doc.Id, true, "Q1"),
            DocumentMessage.Create(doc.Id, false, "A1")
        ]);

        Assert.Equal(2, await ctx.DocumentMessages.CountAsync());
    }

    [Fact]
    public async Task GetByDocumentIdAsync_ReturnsOrdered()
    {
        await using var ctx = CreateContext();
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        var repo = new DocumentMessageRepository(ctx);
        await repo.AddAsync(DocumentMessage.Create(doc.Id, true, "Q1"));
        await repo.AddAsync(DocumentMessage.Create(doc.Id, false, "A1"));

        var result = await repo.GetByDocumentIdAsync(doc.Id);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetByDocumentIdAsync_NoMessages_Empty()
    {
        await using var ctx = CreateContext();
        var repo = new DocumentMessageRepository(ctx);
        var result = await repo.GetByDocumentIdAsync(Guid.NewGuid());
        Assert.Empty(result);
    }
}
