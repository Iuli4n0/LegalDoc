using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Persistence;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(256);
            entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(u => u.FullName).IsRequired().HasMaxLength(200);
            entity.Property(u => u.CreatedAt).IsRequired();
            entity.Property(u => u.LastLoginAt);
            entity.Property(u => u.Role).IsRequired().HasMaxLength(50).HasDefaultValue("User");
            entity.Property(u => u.TotalDocumentsUploaded).IsRequired().HasDefaultValue(0);
            entity.Property(u => u.MaxDocuments).IsRequired().HasDefaultValue(3);
            entity.Property(u => u.MaxDocumentSizeMb).IsRequired().HasDefaultValue(10);
            entity.HasIndex(u => u.Email).IsUnique();
        });
    }
}
