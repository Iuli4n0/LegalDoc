using System;

namespace IdentityService.Domain.Entities;

public class User
{
    private User()
    {
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }

    public static User Create(string email, string passwordHash, string fullName)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            FullName = fullName,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = null
        };
    }

    public void UpdateLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }
}

