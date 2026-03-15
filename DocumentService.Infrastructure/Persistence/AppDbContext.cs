using DocumentService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocumentService.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Clause> Clauses => Set<Clause>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
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

            entity.HasOne(c => c.Document)
                .WithMany()
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

