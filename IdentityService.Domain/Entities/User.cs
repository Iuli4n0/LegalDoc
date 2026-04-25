using System;

namespace IdentityService.Domain.Entities;

public class User
{
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
    public int MaxDocuments { get; private set; } = 1;
    public int MaxDocumentSizeMb { get; private set; } = 1;

    // ── Subscription ───────────────────────────────────────
    public SubscriptionPlan SubscriptionPlan { get; private set; } = SubscriptionPlan.Free;
    public string? StripeCustomerId { get; private set; }
    public string? StripeSubscriptionId { get; private set; }
    public int MonthlyDocumentsUploaded { get; private set; }
    public DateTime CurrentPeriodEnd { get; private set; } = DateTime.UtcNow.AddMonths(1);

    // ── Plan limits mapping ────────────────────────────────
    private static (int maxDocs, int maxSizeMb) GetPlanLimits(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Free   => (1, 1),
        SubscriptionPlan.Bronze => (5, 1),
        SubscriptionPlan.Silver => (25, 3),
        SubscriptionPlan.Gold   => (100, 10),
        _ => (1, 1)
    };

    public static User Create(string email, string passwordHash, string fullName)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty.", nameof(email));
        
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Password hash cannot be empty.", nameof(passwordHash));
        
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("Full name cannot be empty.", nameof(fullName));
        
        var (maxDocs, maxSizeMb) = GetPlanLimits(SubscriptionPlan.Free);

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
            MonthlyDocumentsUploaded = 0,
            MaxDocuments = maxDocs,
            MaxDocumentSizeMb = maxSizeMb,
            SubscriptionPlan = SubscriptionPlan.Free,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1)
        };
    }

    public void UpdateLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    public void IncrementDocumentCount()
    {
        ResetMonthlyCounterIfNeeded();
        TotalDocumentsUploaded++;
        MonthlyDocumentsUploaded++;
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

    public void UpdateSubscription(SubscriptionPlan plan, string? stripeSubscriptionId = null)
    {
        SubscriptionPlan = plan;
        StripeSubscriptionId = stripeSubscriptionId;

        var (maxDocs, maxSizeMb) = GetPlanLimits(plan);
        MaxDocuments = maxDocs;
        MaxDocumentSizeMb = maxSizeMb;
    }

    public void SetStripeCustomerId(string customerId)
    {
        if (string.IsNullOrWhiteSpace(customerId))
            throw new ArgumentException("Stripe customer ID cannot be empty.", nameof(customerId));
        StripeCustomerId = customerId;
    }

    public void SetCurrentPeriodEnd(DateTime periodEnd)
    {
        CurrentPeriodEnd = periodEnd;
    }

    public void ResetMonthlyCounter()
    {
        MonthlyDocumentsUploaded = 0;
        CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1);
    }

    public void SetRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be empty.", nameof(role));

        Role = role;
    }

    public bool CanUploadDocument()
    {
        ResetMonthlyCounterIfNeeded();
        return MonthlyDocumentsUploaded < MaxDocuments;
    }

    public int GetRemainingUploads()
    {
        ResetMonthlyCounterIfNeeded();
        return Math.Max(0, MaxDocuments - MonthlyDocumentsUploaded);
    }

    private void ResetMonthlyCounterIfNeeded()
    {
        if (DateTime.UtcNow >= CurrentPeriodEnd)
        {
            MonthlyDocumentsUploaded = 0;
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1);
        }
    }
}
