using DocumentService.Domain.Entities;
using DocumentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocumentService.Test.Infrastructure.Persistence;

public class AppDbContextTests
{
    [Fact]
    public async Task Given_Context_When_DocumentEntityIsUsed_Then_ModelConfigurationIsApplied()
    {
        await using var context = CreateContext();

        var entity = context.Model.FindEntityType(typeof(Document));

        Assert.NotNull(entity);
        Assert.Equal("Document", context.Documents.EntityType.GetDefaultTableName());
        Assert.Equal(450, entity.FindProperty(nameof(Document.UserId))!.GetMaxLength());
        Assert.Equal(500, entity.FindProperty(nameof(Document.FileName))!.GetMaxLength());
        Assert.Equal(200, entity.FindProperty(nameof(Document.ContentType))!.GetMaxLength());
        Assert.Equal(1000, entity.FindProperty(nameof(Document.S3Key))!.GetMaxLength());
        Assert.NotNull(entity.FindIndex(entity.FindProperty(nameof(Document.UserId))!));

        var doc = Document.Create("x.pdf", "application/pdf", "k", 1, "user-1");
        await context.Documents.AddAsync(doc);
        await context.SaveChangesAsync();

        var loaded = await context.Documents.SingleAsync();
        Assert.Equal(doc.Id, loaded.Id);
    }

    private static TestAppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestAppDbContext(options);
    }
}

/// <summary>
/// Test-specific DbContext that ignores the pgvector Embedding property
/// which is not supported by the InMemory provider.
/// </summary>
internal class TestAppDbContext : AppDbContext
{
    public TestAppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<DocumentChunk>().Ignore(c => c.Embedding);
    }
}
