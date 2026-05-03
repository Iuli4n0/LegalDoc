using IdentityService.Domain.Entities;

namespace IdentityService.Test.Domain;

public class UserExtraTests
{
    [Fact]
    public void IncrementDocumentCount_IncrementsCounters()
    {
        var user = User.Create("u@t.com", "h", "N");
        user.IncrementDocumentCount();
        Assert.Equal(1, user.TotalDocumentsUploaded);
        Assert.Equal(1, user.MonthlyDocumentsUploaded);
    }

    [Fact]
    public void UpdateLimits_Valid_SetsValues()
    {
        var user = User.Create("u@t.com", "h", "N");
        user.UpdateLimits(10, 5);
        Assert.Equal(10, user.MaxDocuments);
        Assert.Equal(5, user.MaxDocumentSizeMb);
    }

    [Fact]
    public void UpdateLimits_NegativeMaxDocs_Throws()
    {
        var user = User.Create("u@t.com", "h", "N");
        Assert.Throws<ArgumentException>(() => user.UpdateLimits(-1, 1));
    }

    [Fact]
    public void UpdateLimits_ZeroMaxSize_Throws()
    {
        var user = User.Create("u@t.com", "h", "N");
        Assert.Throws<ArgumentException>(() => user.UpdateLimits(1, 0));
    }

    [Fact]
    public void UpdateLimits_ZeroMaxDocs_Succeeds()
    {
        var user = User.Create("u@t.com", "h", "N");
        user.UpdateLimits(0, 1);
        Assert.Equal(0, user.MaxDocuments);
    }

    [Theory]
    [InlineData(SubscriptionPlan.Bronze, 5, 1)]
    [InlineData(SubscriptionPlan.Silver, 25, 3)]
    [InlineData(SubscriptionPlan.Gold, 100, 10)]
    [InlineData(SubscriptionPlan.Free, 1, 1)]
    public void UpdateSubscription_SetsLimitsForPlan(SubscriptionPlan plan, int expectedDocs, int expectedSize)
    {
        var user = User.Create("u@t.com", "h", "N");
        user.UpdateSubscription(plan, "sub_id");
        Assert.Equal(plan, user.SubscriptionPlan);
        Assert.Equal(expectedDocs, user.MaxDocuments);
        Assert.Equal(expectedSize, user.MaxDocumentSizeMb);
        Assert.Equal("sub_id", user.StripeSubscriptionId);
    }

    [Fact]
    public void UpdateSubscription_WithoutSubId()
    {
        var user = User.Create("u@t.com", "h", "N");
        user.UpdateSubscription(SubscriptionPlan.Bronze);
        Assert.Null(user.StripeSubscriptionId);
    }

    [Fact]
    public void SetStripeCustomerId_Valid()
    {
        var user = User.Create("u@t.com", "h", "N");
        user.SetStripeCustomerId("cus_123");
        Assert.Equal("cus_123", user.StripeCustomerId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetStripeCustomerId_Invalid_Throws(string? id)
    {
        var user = User.Create("u@t.com", "h", "N");
        Assert.Throws<ArgumentException>(() => user.SetStripeCustomerId(id!));
    }

    [Fact]
    public void SetCurrentPeriodEnd_SetsValue()
    {
        var user = User.Create("u@t.com", "h", "N");
        var dt = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        user.SetCurrentPeriodEnd(dt);
        Assert.Equal(dt, user.CurrentPeriodEnd);
    }

    [Fact]
    public void ResetMonthlyCounter_ResetsAndSetsPeriodEnd()
    {
        var user = User.Create("u@t.com", "h", "N");
        user.IncrementDocumentCount();
        user.ResetMonthlyCounter();
        Assert.Equal(0, user.MonthlyDocumentsUploaded);
    }

    [Fact]
    public void SetRole_Valid()
    {
        var user = User.Create("u@t.com", "h", "N");
        user.SetRole("Admin");
        Assert.Equal("Admin", user.Role);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetRole_Invalid_Throws(string? role)
    {
        var user = User.Create("u@t.com", "h", "N");
        Assert.Throws<ArgumentException>(() => user.SetRole(role!));
    }

    [Fact]
    public void CanUploadDocument_UnderLimit_True()
    {
        var user = User.Create("u@t.com", "h", "N");
        Assert.True(user.CanUploadDocument());
    }

    [Fact]
    public void CanUploadDocument_AtLimit_False()
    {
        var user = User.Create("u@t.com", "h", "N"); // Free = max 1
        user.IncrementDocumentCount();
        Assert.False(user.CanUploadDocument());
    }

    [Fact]
    public void GetRemainingUploads_UnderLimit()
    {
        var user = User.Create("u@t.com", "h", "N"); // Free = max 1
        Assert.Equal(1, user.GetRemainingUploads());
    }

    [Fact]
    public void GetRemainingUploads_AtLimit_Zero()
    {
        var user = User.Create("u@t.com", "h", "N");
        user.IncrementDocumentCount();
        Assert.Equal(0, user.GetRemainingUploads());
    }

    [Fact]
    public void DefaultPlan_Free()
    {
        var user = User.Create("u@t.com", "h", "N");
        Assert.Equal(SubscriptionPlan.Free, user.SubscriptionPlan);
        Assert.Equal(1, user.MaxDocuments);
        Assert.Equal(1, user.MaxDocumentSizeMb);
    }

    [Fact]
    public void NullEmail_Throws()
    {
        Assert.Throws<ArgumentException>(() => User.Create(null!, "h", "N"));
    }

    [Fact]
    public void NullPasswordHash_Throws()
    {
        Assert.Throws<ArgumentException>(() => User.Create("u@t.com", null!, "N"));
    }

    [Fact]
    public void NullFullName_Throws()
    {
        Assert.Throws<ArgumentException>(() => User.Create("u@t.com", "h", null!));
    }
}
