using IdentityService.Domain.Entities;

namespace IdentityService.Test.Domain;

public class UserTests
{
    [Fact]
    public void Given_ValidParameters_When_CreateIsCalled_Then_ShouldCreateUserWithCorrectProperties()
    {
        // Arrange
        var email = "Test@Example.com";
        var passwordHash = "hashed_password_123";
        var fullName = "Ion Popescu";

        // Act
        var user = User.Create(email, passwordHash, fullName);

        // Assert
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("test@example.com", user.Email); // email should be lowercased
        Assert.Equal(passwordHash, user.PasswordHash);
        Assert.Equal(fullName, user.FullName);
        Assert.True(user.CreatedAt <= DateTime.UtcNow);
        Assert.Null(user.LastLoginAt);
    }

    [Fact]
    public void Given_ValidParameters_When_CreateIsCalledTwice_Then_ShouldGenerateUniqueIds()
    {
        // Arrange & Act
        var user1 = User.Create("user1@test.com", "hash1", "User One");
        var user2 = User.Create("user2@test.com", "hash2", "User Two");

        // Assert
        Assert.NotEqual(user1.Id, user2.Id);
    }

    [Fact]
    public void Given_EmailWithUpperCase_When_CreateIsCalled_Then_ShouldNormalizeEmailToLowerCase()
    {
        // Arrange
        var email = "John.Doe@COMPANY.COM";

        // Act
        var user = User.Create(email, "hash", "John Doe");

        // Assert
        Assert.Equal("john.doe@company.com", user.Email);
    }

    [Fact]
    public void Given_NewlyCreatedUser_When_UpdateLastLoginIsCalled_Then_ShouldSetLastLoginAtToCurrentTime()
    {
        // Arrange
        var user = User.Create("user@test.com", "hash", "Test User");
        Assert.Null(user.LastLoginAt);

        // Act
        user.UpdateLastLogin();

        // Assert
        Assert.NotNull(user.LastLoginAt);
        Assert.True(user.LastLoginAt <= DateTime.UtcNow);
        Assert.True(user.LastLoginAt >= DateTime.UtcNow.AddSeconds(-2));
    }

    [Fact]
    public void Given_UserWithExistingLastLogin_When_UpdateLastLoginIsCalled_Then_ShouldUpdateLastLoginAt()
    {
        // Arrange
        var user = User.Create("user@test.com", "hash", "Test User");
        user.UpdateLastLogin();
        var firstLogin = user.LastLoginAt;

        // Act
        user.UpdateLastLogin();

        // Assert
        Assert.NotNull(user.LastLoginAt);
        Assert.True(user.LastLoginAt >= firstLogin);
    }

    [Fact]
    public void Given_ValidParameters_When_CreateIsCalled_Then_ShouldSetCreatedAtToCurrentUtcTime()
    {
        // Arrange
        var beforeCreation = DateTime.UtcNow;

        // Act
        var user = User.Create("user@test.com", "hash", "Test User");

        // Assert
        var afterCreation = DateTime.UtcNow;
        Assert.True(user.CreatedAt >= beforeCreation);
        Assert.True(user.CreatedAt <= afterCreation);
    }
}

