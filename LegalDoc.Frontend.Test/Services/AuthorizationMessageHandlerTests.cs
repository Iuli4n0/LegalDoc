using System.Net;
using LegalDoc.Frontend.Services;

namespace LegalDoc.Frontend.Test.Services;

public class AuthorizationMessageHandlerTests
{
    private readonly AuthStateService _authState;

    public AuthorizationMessageHandlerTests()
    {
        _authState = new AuthStateService();
    }

    private static async Task<HttpResponseMessage> InvokeHandler(
        AuthorizationMessageHandler handler, HttpRequestMessage request)
    {
        var innerHandler = new FakeInnerHandler();
        handler.InnerHandler = innerHandler;

        var invoker = new HttpMessageInvoker(handler);
        return await invoker.SendAsync(request, CancellationToken.None);
    }

    [Fact]
    public async Task Given_AuthenticatedUser_When_SendAsyncIsCalled_Then_ShouldAddBearerAuthorizationHeader()
    {
        // Arrange
        _authState.SetAuthenticated("Ion Popescu", "ion@example.com", "my-jwt-token");
        var handler = new AuthorizationMessageHandler(_authState);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/test");

        // Act
        await InvokeHandler(handler, request);

        // Assert
        Assert.NotNull(request.Headers.Authorization);
        Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
        Assert.Equal("my-jwt-token", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task Given_UnauthenticatedUser_When_SendAsyncIsCalled_Then_ShouldNotAddAuthorizationHeader()
    {
        // Arrange
        var handler = new AuthorizationMessageHandler(_authState);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/test");

        // Act
        await InvokeHandler(handler, request);

        // Assert
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task Given_NullToken_When_SendAsyncIsCalled_Then_ShouldNotAddAuthorizationHeader()
    {
        // Arrange
        _authState.SetToken(null);
        var handler = new AuthorizationMessageHandler(_authState);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/test");

        // Act
        await InvokeHandler(handler, request);

        // Assert
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task Given_EmptyToken_When_SendAsyncIsCalled_Then_ShouldNotAddAuthorizationHeader()
    {
        // Arrange
        _authState.SetToken(string.Empty);
        var handler = new AuthorizationMessageHandler(_authState);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/test");

        // Act
        await InvokeHandler(handler, request);

        // Assert
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task Given_AuthenticatedUser_When_SendAsyncIsCalled_Then_ShouldPassRequestToInnerHandler()
    {
        // Arrange
        _authState.SetAuthenticated("Ion Popescu", "ion@example.com", "my-jwt-token");
        var handler = new AuthorizationMessageHandler(_authState);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/test");

        // Act
        var response = await InvokeHandler(handler, request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Given_UserLogsOutAfterLogin_When_SendAsyncIsCalled_Then_ShouldNotAddAuthorizationHeader()
    {
        // Arrange
        _authState.SetAuthenticated("Ion Popescu", "ion@example.com", "my-jwt-token");
        _authState.SetLoggedOut();
        var handler = new AuthorizationMessageHandler(_authState);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/test");

        // Act
        await InvokeHandler(handler, request);

        // Assert
        Assert.Null(request.Headers.Authorization);
    }

    /// <summary>
    /// Fake inner handler that returns a 200 OK response to simulate pipeline behavior.
    /// </summary>
    private class FakeInnerHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}

