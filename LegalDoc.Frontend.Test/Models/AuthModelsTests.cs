using LegalDoc.Frontend.Models;

namespace LegalDoc.Frontend.Test.Models;

public class AuthModelsTests
{
    [Fact]
    public void Given_NewLoginRequest_When_Created_Then_ShouldHaveDefaultEmptyValues()
    {
        // Arrange & Act
        var request = new LoginRequest();

        // Assert
        Assert.Equal(string.Empty, request.Email);
        Assert.Equal(string.Empty, request.Password);
    }

    [Fact]
    public void Given_LoginRequest_When_PropertiesAreSet_Then_ShouldRetainValues()
    {
        // Arrange
        var request = new LoginRequest();

        // Act
        request.Email = "ion@example.com";
        request.Password = "parola123";

        // Assert
        Assert.Equal("ion@example.com", request.Email);
        Assert.Equal("parola123", request.Password);
    }

    [Fact]
    public void Given_NewRegisterRequest_When_Created_Then_ShouldHaveDefaultEmptyValues()
    {
        // Arrange & Act
        var request = new RegisterRequest();

        // Assert
        Assert.Equal(string.Empty, request.Email);
        Assert.Equal(string.Empty, request.Password);
        Assert.Equal(string.Empty, request.FullName);
    }

    [Fact]
    public void Given_RegisterRequest_When_PropertiesAreSet_Then_ShouldRetainValues()
    {
        // Arrange
        var request = new RegisterRequest();

        // Act
        request.Email = "ion@example.com";
        request.Password = "parola123";
        request.FullName = "Ion Popescu";

        // Assert
        Assert.Equal("ion@example.com", request.Email);
        Assert.Equal("parola123", request.Password);
        Assert.Equal("Ion Popescu", request.FullName);
    }

    [Fact]
    public void Given_ValidParameters_When_LoginResponseIsCreated_Then_ShouldStoreAllProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddHours(1);

        // Act
        var response = new LoginResponse(userId, "ion@example.com", "Ion Popescu", "jwt-token", expiresAt);

        // Assert
        Assert.Equal(userId, response.UserId);
        Assert.Equal("ion@example.com", response.Email);
        Assert.Equal("Ion Popescu", response.FullName);
        Assert.Equal("jwt-token", response.Token);
        Assert.Equal(expiresAt, response.ExpiresAt);
    }

    [Fact]
    public void Given_TwoLoginResponsesWithSameData_When_Compared_Then_ShouldBeEqual()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddHours(1);

        // Act
        var response1 = new LoginResponse(userId, "ion@example.com", "Ion Popescu", "jwt-token", expiresAt);
        var response2 = new LoginResponse(userId, "ion@example.com", "Ion Popescu", "jwt-token", expiresAt);

        // Assert
        Assert.Equal(response1, response2);
    }

    [Fact]
    public void Given_TwoLoginResponsesWithDifferentData_When_Compared_Then_ShouldNotBeEqual()
    {
        // Arrange
        var expiresAt = DateTime.UtcNow.AddHours(1);

        // Act
        var response1 = new LoginResponse(Guid.NewGuid(), "ion@example.com", "Ion Popescu", "token1", expiresAt);
        var response2 = new LoginResponse(Guid.NewGuid(), "maria@example.com", "Maria Ionescu", "token2", expiresAt);

        // Assert
        Assert.NotEqual(response1, response2);
    }

    [Fact]
    public void Given_ValidParameters_When_RegisterResponseIsCreated_Then_ShouldStoreAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        // Act
        var response = new RegisterResponse(id, "ion@example.com", "Ion Popescu", createdAt);

        // Assert
        Assert.Equal(id, response.Id);
        Assert.Equal("ion@example.com", response.Email);
        Assert.Equal("Ion Popescu", response.FullName);
        Assert.Equal(createdAt, response.CreatedAt);
    }

    [Fact]
    public void Given_TwoRegisterResponsesWithSameData_When_Compared_Then_ShouldBeEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        // Act
        var response1 = new RegisterResponse(id, "ion@example.com", "Ion Popescu", createdAt);
        var response2 = new RegisterResponse(id, "ion@example.com", "Ion Popescu", createdAt);

        // Assert
        Assert.Equal(response1, response2);
    }
}

