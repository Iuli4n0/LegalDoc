using DocumentService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace DocumentService.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Clause> Clauses => Set<Clause>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<DocumentMessage> DocumentMessages => Set<DocumentMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.UserId).IsRequired().HasMaxLength(450);
            entity.HasIndex(d => d.UserId);
            entity.Property(d => d.FileName).IsRequired().HasMaxLength(500);
            entity.Property(d => d.ContentType).IsRequired().HasMaxLength(200);
            entity.Property(d => d.S3Key).IsRequired().HasMaxLength(1000);
            entity.Property(d => d.FileSize).IsRequired();
            entity.Property(d => d.UploadedAt).IsRequired();
            entity.Property(d => d.Resume).HasColumnType("text");
            entity.Property(d => d.ResumeGeneratedAt);
        });

        modelBuilder.Entity<Clause>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Text).IsRequired();
            entity.Property(c => c.ExtractedAt).IsRequired();
            entity.Property(c => c.AbusiveProbability);
            entity.Property(c => c.IsAbusive);
            entity.Property(c => c.ClassifiedAt);

            entity.HasOne(c => c.Document)
                .WithMany()
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.ChunkIndex).IsRequired();
            entity.Property(c => c.Text).IsRequired();
            entity.Property(c => c.CreatedAt).IsRequired();

            entity.HasIndex(c => c.DocumentId);

            entity.HasOne(c => c.Document)
                .WithMany()
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.DocumentId).IsRequired();
            entity.Property(m => m.IsUser).IsRequired();
            entity.Property(m => m.Text).IsRequired();
            entity.Property(m => m.SourcesJson);
            entity.Property(m => m.CreatedAt).IsRequired();

            entity.HasIndex(m => m.DocumentId);

            entity.HasOne(m => m.Document)
                .WithMany()
                .HasForeignKey(m => m.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

