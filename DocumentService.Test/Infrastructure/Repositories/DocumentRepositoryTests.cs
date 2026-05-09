using DocumentService.Domain.Entities;
using DocumentService.Infrastructure.Persistence;
using DocumentService.Infrastructure.Repositories;
using DocumentService.Test.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocumentService.Test.Infrastructure.Repositories;

public class DocumentRepositoryTests
{
    [Fact]
    public async Task Given_Document_When_AddAsync_Then_DocumentIsPersisted()
    {
        await using var context = CreateContext();
        var repository = new DocumentRepository(context);
        var document = Document.Create("a.pdf", "application/pdf", "k1", 10, "user-1");

        await repository.AddAsync(document);

        var saved = await context.Documents.SingleAsync().ConfigureAwait(false);
        Assert.Equal(document.Id, saved.Id);
    }

    [Fact]
    public async Task Given_ExistingId_When_GetByIdAsync_Then_DocumentIsReturned()
    {
        await using var context = CreateContext();
        var document = Document.Create("a.pdf", "application/pdf", "k1", 10, "user-1");
        await context.Documents.AddAsync(document).ConfigureAwait(false);
        await context.SaveChangesAsync().ConfigureAwait(false);

        var repository = new DocumentRepository(context);

        var result = await repository.GetByIdAsync(document.Id);

        Assert.NotNull(result);
        Assert.Equal(document.Id, result.Id);
    }

    [Fact]
    public async Task Given_MissingId_When_GetByIdAsync_Then_NullIsReturned()
    {
        await using var context = CreateContext();
        var repository = new DocumentRepository(context);

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task Given_ExistingDocument_When_UpdateAsync_Then_ChangesAreSaved()
    {
        await using var context = CreateContext();
        var document = Document.Create("a.pdf", "application/pdf", "k1", 10, "user-1");
        await context.Documents.AddAsync(document).ConfigureAwait(false);
        await context.SaveChangesAsync().ConfigureAwait(false);

        document.SetResume("updated");
        var repository = new DocumentRepository(context);

        await repository.UpdateAsync(document);

        var updated = await context.Documents.SingleAsync().ConfigureAwait(false);
        Assert.Equal("updated", updated.Resume);
        Assert.NotNull(updated.ResumeGeneratedAt);
    }

    private static TestAppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestAppDbContext(options);
    }
}

