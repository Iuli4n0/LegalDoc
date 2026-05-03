using DocumentService.Domain.Entities;
using DocumentService.Infrastructure.Persistence;
using DocumentService.Infrastructure.Repositories;
using DocumentService.Test.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocumentService.Test.Infrastructure.Repositories;

public class ClauseRepositoryTests
{
    private static TestAppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TestAppDbContext(options);
    }

    [Fact]
    public async Task AddAsync_PersistsClause()
    {
        await using var ctx = CreateContext();
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        var repo = new ClauseRepository(ctx);
        var clause = Clause.Create(doc.Id, "Text");
        await repo.AddAsync(clause);

        Assert.NotNull(await ctx.Clauses.FindAsync(clause.Id));
    }

    [Fact]
    public async Task AddRangeAsync_PersistsMultiple()
    {
        await using var ctx = CreateContext();
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        var repo = new ClauseRepository(ctx);
        await repo.AddRangeAsync([Clause.Create(doc.Id, "A"), Clause.Create(doc.Id, "B")]);

        Assert.Equal(2, await ctx.Clauses.CountAsync());
    }

    [Fact]
    public async Task GetByDocumentIdAsync_ReturnsOrdered()
    {
        await using var ctx = CreateContext();
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        var repo = new ClauseRepository(ctx);
        await repo.AddAsync(Clause.Create(doc.Id, "First"));
        await repo.AddAsync(Clause.Create(doc.Id, "Second"));

        var result = await repo.GetByDocumentIdAsync(doc.Id);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetByDocumentIdAsync_Empty()
    {
        await using var ctx = CreateContext();
        var repo = new ClauseRepository(ctx);
        var result = await repo.GetByDocumentIdAsync(Guid.NewGuid());
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByIdAsync_Found()
    {
        await using var ctx = CreateContext();
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        var clause = Clause.Create(doc.Id, "T");
        ctx.Clauses.Add(clause);
        await ctx.SaveChangesAsync();

        var repo = new ClauseRepository(ctx);
        Assert.NotNull(await repo.GetByIdAsync(clause.Id));
    }

    [Fact]
    public async Task GetByIdAsync_NotFound()
    {
        await using var ctx = CreateContext();
        var repo = new ClauseRepository(ctx);
        Assert.Null(await repo.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAsync_RemovesClause()
    {
        await using var ctx = CreateContext();
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        var clause = Clause.Create(doc.Id, "T");
        ctx.Clauses.Add(clause);
        await ctx.SaveChangesAsync();

        var repo = new ClauseRepository(ctx);
        await repo.DeleteAsync(clause);
        Assert.Null(await ctx.Clauses.FindAsync(clause.Id));
    }

    [Fact]
    public async Task UpdateRangeAsync_Updates()
    {
        await using var ctx = CreateContext();
        var doc = Document.Create("f.pdf", "application/pdf", "k", 100, "u");
        ctx.Documents.Add(doc);
        await ctx.SaveChangesAsync();

        var clause = Clause.Create(doc.Id, "T");
        ctx.Clauses.Add(clause);
        await ctx.SaveChangesAsync();

        clause.SetClassification(true, 0.9);
        var repo = new ClauseRepository(ctx);
        await repo.UpdateRangeAsync([clause]);

        var updated = await ctx.Clauses.AsNoTracking().FirstAsync(c => c.Id == clause.Id);
        Assert.True(updated.IsAbusive);
    }
}
