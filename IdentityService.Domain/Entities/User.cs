using System;

namespace IdentityService.Domain.Entities;

public class User
{
    private const int DefaultMaxDocuments = 3;
    private const int DefaultMaxDocumentSizeMb = 10;
    private const string RoleUser = "User";

    private User()
    {
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public string Role { get; private set; } = RoleUser;
    public int TotalDocumentsUploaded { get; private set; }
    public int MaxDocuments { get; private set; } = DefaultMaxDocuments;
    public int MaxDocumentSizeMb { get; private set; } = DefaultMaxDocumentSizeMb;

    public static User Create(string email, string passwordHash, string fullName)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.", nameof(email));
        
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be empty.", nameof(passwordHash));
        
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name cannot be empty.", nameof(fullName));
        
        return new User
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            FullName = fullName,
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = null,
            Role = RoleUser,
            TotalDocumentsUploaded = 0,
            MaxDocuments = DefaultMaxDocuments,
            MaxDocumentSizeMb = DefaultMaxDocumentSizeMb
        };
    }

    public void UpdateLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    public void IncrementDocumentCount()
    {
        TotalDocumentsUploaded++;
    }

    public void UpdateLimits(int maxDocuments, int maxDocumentSizeMb)
    {
        if (maxDocuments < 0)
            throw new ArgumentException("Max documents cannot be negative.", nameof(maxDocuments));
        if (maxDocumentSizeMb <= 0)
            throw new ArgumentException("Max document size must be greater than zero.", nameof(maxDocumentSizeMb));

        MaxDocuments = maxDocuments;
        MaxDocumentSizeMb = maxDocumentSizeMb;
    }

    public void SetRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be empty.", nameof(role));

        Role = role;
    }

    public bool CanUploadDocument() => TotalDocumentsUploaded < MaxDocuments;
}
