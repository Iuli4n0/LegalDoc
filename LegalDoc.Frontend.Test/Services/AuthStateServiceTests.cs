using LegalDoc.Frontend.Services;

namespace LegalDoc.Frontend.Test.Services;

public class AuthStateServiceTests
{
    [Fact]
    public void Given_NewAuthStateService_When_Created_Then_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var authState = new AuthStateService();

        // Assert
        Assert.False(authState.IsAuthenticated);
        Assert.Equal(string.Empty, authState.UserName);
        Assert.Equal(string.Empty, authState.UserEmail);
        Assert.Null(authState.Token);
    }

    [Fact]
    public void Given_UnauthenticatedState_When_SetAuthenticatedIsCalled_Then_ShouldSetIsAuthenticatedToTrue()
    {
        // Arrange
        var authState = new AuthStateService();

        // Act
        authState.SetAuthenticated("Ion Popescu", "ion@example.com", "test-token");

        // Assert
        Assert.True(authState.IsAuthenticated);
    }

    [Fact]
    public void Given_UnauthenticatedState_When_SetAuthenticatedIsCalled_Then_ShouldSetUserName()
    {
        // Arrange
        var authState = new AuthStateService();

        // Act
        authState.SetAuthenticated("Ion Popescu", "ion@example.com", "test-token");

        // Assert
        Assert.Equal("Ion Popescu", authState.UserName);
    }

    [Fact]
    public void Given_UnauthenticatedState_When_SetAuthenticatedIsCalled_Then_ShouldSetUserEmail()
    {
        // Arrange
        var authState = new AuthStateService();

        // Act
        authState.SetAuthenticated("Ion Popescu", "ion@example.com", "test-token");

        // Assert
        Assert.Equal("ion@example.com", authState.UserEmail);
    }

    [Fact]
    public void Given_UnauthenticatedState_When_SetAuthenticatedIsCalled_Then_ShouldSetToken()
    {
        // Arrange
        var authState = new AuthStateService();

        // Act
        authState.SetAuthenticated("Ion Popescu", "ion@example.com", "test-token");

        // Assert
        Assert.Equal("test-token", authState.Token);
    }

    [Fact]
    public void Given_UnauthenticatedState_When_SetAuthenticatedIsCalledWithoutToken_Then_ShouldHaveNullToken()
    {
        // Arrange
        var authState = new AuthStateService();

        // Act
        authState.SetAuthenticated("Ion Popescu", "ion@example.com");

        // Assert
        Assert.Null(authState.Token);
    }

    [Fact]
    public void Given_UnauthenticatedState_When_SetAuthenticatedIsCalled_Then_ShouldInvokeOnChange()
    {
        // Arrange
        var authState = new AuthStateService();
        var eventFired = false;
        authState.OnChange += () => eventFired = true;

        // Act
        authState.SetAuthenticated("Ion Popescu", "ion@example.com", "test-token");

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public void Given_AuthenticatedState_When_SetLoggedOutIsCalled_Then_ShouldSetIsAuthenticatedToFalse()
    {
        // Arrange
        var authState = new AuthStateService();
        authState.SetAuthenticated("Ion Popescu", "ion@example.com", "test-token");

        // Act
        authState.SetLoggedOut();

        // Assert
        Assert.False(authState.IsAuthenticated);
    }

    [Fact]
    public void Given_AuthenticatedState_When_SetLoggedOutIsCalled_Then_ShouldClearUserName()
    {
        // Arrange
        var authState = new AuthStateService();
        authState.SetAuthenticated("Ion Popescu", "ion@example.com", "test-token");

        // Act
        authState.SetLoggedOut();

        // Assert
        Assert.Equal(string.Empty, authState.UserName);
    }

    [Fact]
    public void Given_AuthenticatedState_When_SetLoggedOutIsCalled_Then_ShouldClearUserEmail()
    {
        // Arrange
        var authState = new AuthStateService();
        authState.SetAuthenticated("Ion Popescu", "ion@example.com", "test-token");

        // Act
        authState.SetLoggedOut();

        // Assert
        Assert.Equal(string.Empty, authState.UserEmail);
    }

    [Fact]
    public void Given_AuthenticatedState_When_SetLoggedOutIsCalled_Then_ShouldClearToken()
    {
        // Arrange
        var authState = new AuthStateService();
        authState.SetAuthenticated("Ion Popescu", "ion@example.com", "test-token");

        // Act
        authState.SetLoggedOut();

        // Assert
        Assert.Null(authState.Token);
    }

    [Fact]
    public void Given_AuthenticatedState_When_SetLoggedOutIsCalled_Then_ShouldInvokeOnChange()
    {
        // Arrange
        var authState = new AuthStateService();
        authState.SetAuthenticated("Ion Popescu", "ion@example.com", "test-token");
        var eventFired = false;
        authState.OnChange += () => eventFired = true;

        // Act
        authState.SetLoggedOut();

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public void Given_ExistingState_When_SetTokenIsCalled_Then_ShouldUpdateToken()
    {
        // Arrange
        var authState = new AuthStateService();

        // Act
        authState.SetToken("new-token");

        // Assert
        Assert.Equal("new-token", authState.Token);
    }

    [Fact]
    public void Given_ExistingToken_When_SetTokenIsCalledWithNull_Then_ShouldSetTokenToNull()
    {
        // Arrange
        var authState = new AuthStateService();
        authState.SetToken("old-token");

        // Act
        authState.SetToken(null);

        // Assert
        Assert.Null(authState.Token);
    }

    [Fact]
    public void Given_MultipleSubscribers_When_SetAuthenticatedIsCalled_Then_ShouldNotifyAllSubscribers()
    {
        // Arrange
        var authState = new AuthStateService();
        var subscriber1Notified = false;
        var subscriber2Notified = false;
        authState.OnChange += () => subscriber1Notified = true;
        authState.OnChange += () => subscriber2Notified = true;

        // Act
        authState.SetAuthenticated("Ion Popescu", "ion@example.com", "test-token");

        // Assert
        Assert.True(subscriber1Notified);
        Assert.True(subscriber2Notified);
    }

    [Fact]
    public void Given_NoSubscribers_When_SetAuthenticatedIsCalled_Then_ShouldNotThrow()
    {
        // Arrange
        var authState = new AuthStateService();

        // Act
        var exception = Record.Exception(() => authState.SetAuthenticated("Ion Popescu", "ion@example.com"));

        // Assert
        Assert.Null(exception);
    }

    [Fact]
    public void Given_NoSubscribers_When_SetLoggedOutIsCalled_Then_ShouldNotThrow()
    {
        // Arrange
        var authState = new AuthStateService();

        // Act
        var exception = Record.Exception(() => authState.SetLoggedOut());

        // Assert
        Assert.Null(exception);
    }
}

