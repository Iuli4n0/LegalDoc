using System.Net;
using System.Net.Http.Json;
using LegalDoc.Frontend.Models;
using LegalDoc.Frontend.Services;
using Moq;

namespace LegalDoc.Frontend.Test.Services;

public class AuthServiceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IAuthStorage> _authStorageMock;
    private readonly AuthStateService _authState;

    public AuthServiceTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _authStorageMock = new Mock<IAuthStorage>();
        _authState = new AuthStateService();
    }

    [Fact]
    public async Task Given_ValidLogin_When_LoginAsync_Then_ShouldPersistAuthDataAndUpdateState()
    {
        var responseModel = new LoginResponse(
            Guid.NewGuid(),
            "ion@example.com",
            "Ion Popescu",
            "token-123",
            DateTime.UtcNow.AddHours(1));

        var client = CreateClient(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responseModel)
            });

        _httpClientFactoryMock.Setup(f => f.CreateClient("IdentityAPI")).Returns(client);
        _authStorageMock.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var service = new AuthService(_httpClientFactoryMock.Object, _authStorageMock.Object, _authState);

        var result = await service.LoginAsync(new LoginRequest { Email = "ion@example.com", Password = "secret" });

        Assert.Equal(responseModel.Token, result.Token);
        Assert.True(_authState.IsAuthenticated);
        Assert.Equal("Ion Popescu", _authState.UserName);
        Assert.Equal("ion@example.com", _authState.UserEmail);
        Assert.Equal("token-123", _authState.Token);

        _authStorageMock.Verify(s => s.SetAsync("authToken", "token-123"), Times.Once);
        _authStorageMock.Verify(s => s.SetAsync("userName", "Ion Popescu"), Times.Once);
        _authStorageMock.Verify(s => s.SetAsync("userEmail", "ion@example.com"), Times.Once);
    }

    [Fact]
    public async Task Given_UnauthorizedLogin_When_LoginAsync_Then_ShouldThrowUnauthorizedAccessException()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        _httpClientFactoryMock.Setup(f => f.CreateClient("IdentityAPI")).Returns(client);

        var service = new AuthService(_httpClientFactoryMock.Object, _authStorageMock.Object, _authState);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(new LoginRequest { Email = "x@y.com", Password = "bad" }));
    }

    [Fact]
    public async Task Given_InvalidLoginPayload_When_LoginAsync_Then_ShouldThrowException()
    {
        var client = CreateClient(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create<LoginResponse?>(null)
            });

        _httpClientFactoryMock.Setup(f => f.CreateClient("IdentityAPI")).Returns(client);
        var service = new AuthService(_httpClientFactoryMock.Object, _authStorageMock.Object, _authState);

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            service.LoginAsync(new LoginRequest { Email = "ion@example.com", Password = "secret" }));

        Assert.Contains("Răspuns invalid", exception.Message);
    }

    [Fact]
    public async Task Given_ValidRegister_When_RegisterAsync_Then_ShouldReturnRegisterResponse()
    {
        var responseModel = new RegisterResponse(Guid.NewGuid(), "ion@example.com", "Ion Popescu", DateTime.UtcNow);
        var client = CreateClient(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(responseModel)
            });

        _httpClientFactoryMock.Setup(f => f.CreateClient("IdentityAPI")).Returns(client);
        var service = new AuthService(_httpClientFactoryMock.Object, _authStorageMock.Object, _authState);

        var result = await service.RegisterAsync(new RegisterRequest
        {
            Email = "ion@example.com",
            Password = "secret",
            FullName = "Ion Popescu"
        });

        Assert.Equal(responseModel.Id, result.Id);
        Assert.Equal("ion@example.com", result.Email);
    }

    [Fact]
    public async Task Given_ExistingEmail_When_RegisterAsync_Then_ShouldThrowInvalidOperationException()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Conflict));
        _httpClientFactoryMock.Setup(f => f.CreateClient("IdentityAPI")).Returns(client);
        var service = new AuthService(_httpClientFactoryMock.Object, _authStorageMock.Object, _authState);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegisterAsync(new RegisterRequest()));
    }

    [Fact]
    public async Task Given_InvalidRegisterPayload_When_RegisterAsync_Then_ShouldThrowException()
    {
        var client = CreateClient(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create<RegisterResponse?>(null)
            });

        _httpClientFactoryMock.Setup(f => f.CreateClient("IdentityAPI")).Returns(client);
        var service = new AuthService(_httpClientFactoryMock.Object, _authStorageMock.Object, _authState);

        var exception = await Assert.ThrowsAsync<Exception>(() =>
            service.RegisterAsync(new RegisterRequest()));

        Assert.Contains("Răspuns invalid", exception.Message);
    }

    [Fact]
    public async Task Given_AuthenticatedState_When_LogoutAsync_Then_ShouldDeleteStorageAndLogOut()
    {
        _authState.SetAuthenticated("Ion Popescu", "ion@example.com", "token-1");
        _authStorageMock.Setup(s => s.DeleteAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        var service = new AuthService(_httpClientFactoryMock.Object, _authStorageMock.Object, _authState);

        await service.LogoutAsync();

        Assert.False(_authState.IsAuthenticated);
        _authStorageMock.Verify(s => s.DeleteAsync("authToken"), Times.Once);
        _authStorageMock.Verify(s => s.DeleteAsync("userName"), Times.Once);
        _authStorageMock.Verify(s => s.DeleteAsync("userEmail"), Times.Once);
    }

    [Fact]
    public async Task Given_TokenStored_When_GetTokenAsync_Then_ShouldReturnToken()
    {
        _authStorageMock.Setup(s => s.GetAsync("authToken"))
            .ReturnsAsync(new AuthStorageValue(true, "jwt-token"));

        var service = new AuthService(_httpClientFactoryMock.Object, _authStorageMock.Object, _authState);

        var token = await service.GetTokenAsync();

        Assert.Equal("jwt-token", token);
    }

    [Fact]
    public async Task Given_NoTokenStored_When_GetTokenAsync_Then_ShouldReturnNull()
    {
        _authStorageMock.Setup(s => s.GetAsync("authToken"))
            .ReturnsAsync(new AuthStorageValue(false, null));

        var service = new AuthService(_httpClientFactoryMock.Object, _authStorageMock.Object, _authState);

        var token = await service.GetTokenAsync();

        Assert.Null(token);
    }

    [Fact]
    public async Task Given_StorageThrows_When_GetTokenAsync_Then_ShouldReturnNull()
    {
        _authStorageMock.Setup(s => s.GetAsync("authToken"))
            .ThrowsAsync(new Exception("storage failed"));

        var service = new AuthService(_httpClientFactoryMock.Object, _authStorageMock.Object, _authState);

        var token = await service.GetTokenAsync();

        Assert.Null(token);
    }

    [Fact]
    public async Task Given_TokenStored_When_IsAuthenticatedAsync_Then_ShouldReturnTrue()
    {
        _authStorageMock.Setup(s => s.GetAsync("authToken"))
            .ReturnsAsync(new AuthStorageValue(true, "jwt-token"));

        var service = new AuthService(_httpClientFactoryMock.Object, _authStorageMock.Object, _authState);

        var isAuthenticated = await service.IsAuthenticatedAsync();

        Assert.True(isAuthenticated);
    }

    [Fact]
    public async Task Given_EmptyToken_When_IsAuthenticatedAsync_Then_ShouldReturnFalse()
    {
        _authStorageMock.Setup(s => s.GetAsync("authToken"))
            .ReturnsAsync(new AuthStorageValue(true, string.Empty));

        var service = new AuthService(_httpClientFactoryMock.Object, _authStorageMock.Object, _authState);

        var isAuthenticated = await service.IsAuthenticatedAsync();

        Assert.False(isAuthenticated);
    }

    [Fact]
    public async Task Given_ValidStorageData_When_InitializeAsync_Then_ShouldAuthenticateState()
    {
        _authStorageMock.Setup(s => s.GetAsync("authToken"))
            .ReturnsAsync(new AuthStorageValue(true, "token-abc"));
        _authStorageMock.Setup(s => s.GetAsync("userName"))
            .ReturnsAsync(new AuthStorageValue(true, "Ion Popescu"));
        _authStorageMock.Setup(s => s.GetAsync("userEmail"))
            .ReturnsAsync(new AuthStorageValue(true, "ion@example.com"));

        var service = new AuthService(_httpClientFactoryMock.Object, _authStorageMock.Object, _authState);

        await service.InitializeAsync();

        Assert.True(_authState.IsAuthenticated);
        Assert.Equal("Ion Popescu", _authState.UserName);
        Assert.Equal("ion@example.com", _authState.UserEmail);
        Assert.Equal("token-abc", _authState.Token);
    }

    [Fact]
    public async Task Given_MissingProfileValues_When_InitializeAsync_Then_ShouldUseEmptyFallbacks()
    {
        _authStorageMock.Setup(s => s.GetAsync("authToken"))
            .ReturnsAsync(new AuthStorageValue(true, "token-abc"));
        _authStorageMock.Setup(s => s.GetAsync("userName"))
            .ReturnsAsync(new AuthStorageValue(false, null));
        _authStorageMock.Setup(s => s.GetAsync("userEmail"))
            .ReturnsAsync(new AuthStorageValue(false, null));

        var service = new AuthService(_httpClientFactoryMock.Object, _authStorageMock.Object, _authState);

        await service.InitializeAsync();

        Assert.True(_authState.IsAuthenticated);
        Assert.Equal(string.Empty, _authState.UserName);
        Assert.Equal(string.Empty, _authState.UserEmail);
        Assert.Equal("token-abc", _authState.Token);
    }

    [Fact]
    public async Task Given_NoToken_When_InitializeAsync_Then_ShouldSetLoggedOut()
    {
        _authState.SetAuthenticated("Existing User", "old@mail.com", "old-token");
        _authStorageMock.Setup(s => s.GetAsync("authToken"))
            .ReturnsAsync(new AuthStorageValue(false, null));
        _authStorageMock.Setup(s => s.GetAsync("userName"))
            .ReturnsAsync(new AuthStorageValue(true, "Ignored"));
        _authStorageMock.Setup(s => s.GetAsync("userEmail"))
            .ReturnsAsync(new AuthStorageValue(true, "ignored@mail.com"));

        var service = new AuthService(_httpClientFactoryMock.Object, _authStorageMock.Object, _authState);

        await service.InitializeAsync();

        Assert.False(_authState.IsAuthenticated);
        Assert.Null(_authState.Token);
    }

    [Fact]
    public async Task Given_StorageThrows_When_InitializeAsync_Then_ShouldSetLoggedOut()
    {
        _authState.SetAuthenticated("Existing User", "old@mail.com", "old-token");
        _authStorageMock.Setup(s => s.GetAsync("authToken"))
            .ThrowsAsync(new Exception("storage failed"));

        var service = new AuthService(_httpClientFactoryMock.Object, _authStorageMock.Object, _authState);

        await service.InitializeAsync();

        Assert.False(_authState.IsAuthenticated);
        Assert.Null(_authState.Token);
    }

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var httpMessageHandler = new TestHttpMessageHandler(handler);
        return new HttpClient(httpMessageHandler)
        {
            BaseAddress = new Uri("https://localhost")
        };
    }

    private sealed class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}

