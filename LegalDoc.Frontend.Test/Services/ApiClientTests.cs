using System.Net;
using System.Net.Http.Headers;
using LegalDoc.Frontend.Services;
using Moq;

namespace LegalDoc.Frontend.Test.Services;

public class ApiClientTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly AuthStateService _authState;
    private readonly ApiClient _apiClient;

    public ApiClientTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _authState = new AuthStateService();
        _apiClient = new ApiClient(_httpClientFactoryMock.Object, _authState);
    }

    [Fact]
    public void Given_AuthenticatedUser_When_CreateClientIsCalled_Then_ShouldReturnHttpClientWithAuthorizationHeader()
    {
        // Arrange
        _authState.SetAuthenticated("Ion Popescu", "ion@example.com", "my-jwt-token");
        var httpClient = new HttpClient { BaseAddress = new Uri("https://localhost") };
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("API"))
            .Returns(httpClient);

        // Act
        var client = _apiClient.CreateClient();

        // Assert
        Assert.NotNull(client.DefaultRequestHeaders.Authorization);
        Assert.Equal("Bearer", client.DefaultRequestHeaders.Authorization.Scheme);
        Assert.Equal("my-jwt-token", client.DefaultRequestHeaders.Authorization.Parameter);
    }

    [Fact]
    public void Given_UnauthenticatedUser_When_CreateClientIsCalled_Then_ShouldReturnHttpClientWithoutAuthorizationHeader()
    {
        // Arrange
        var httpClient = new HttpClient { BaseAddress = new Uri("https://localhost") };
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("API"))
            .Returns(httpClient);

        // Act
        var client = _apiClient.CreateClient();

        // Assert
        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public void Given_NullToken_When_CreateClientIsCalled_Then_ShouldReturnHttpClientWithoutAuthorizationHeader()
    {
        // Arrange
        _authState.SetToken(null);
        var httpClient = new HttpClient { BaseAddress = new Uri("https://localhost") };
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("API"))
            .Returns(httpClient);

        // Act
        var client = _apiClient.CreateClient();

        // Assert
        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public void Given_EmptyToken_When_CreateClientIsCalled_Then_ShouldReturnHttpClientWithoutAuthorizationHeader()
    {
        // Arrange
        _authState.SetToken(string.Empty);
        var httpClient = new HttpClient { BaseAddress = new Uri("https://localhost") };
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("API"))
            .Returns(httpClient);

        // Act
        var client = _apiClient.CreateClient();

        // Assert
        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public void Given_ValidFactory_When_CreateClientIsCalled_Then_ShouldUseApiNamedClient()
    {
        // Arrange
        var httpClient = new HttpClient { BaseAddress = new Uri("https://localhost") };
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("API"))
            .Returns(httpClient);

        // Act
        _apiClient.CreateClient();

        // Assert
        _httpClientFactoryMock.Verify(f => f.CreateClient("API"), Times.Once);
    }

    [Fact]
    public void Given_UserLogsOutAfterLogin_When_CreateClientIsCalled_Then_ShouldNotIncludeAuthorizationHeader()
    {
        // Arrange
        _authState.SetAuthenticated("Ion Popescu", "ion@example.com", "my-jwt-token");
        _authState.SetLoggedOut();
        var httpClient = new HttpClient { BaseAddress = new Uri("https://localhost") };
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("API"))
            .Returns(httpClient);

        // Act
        var client = _apiClient.CreateClient();

        // Assert
        Assert.Null(client.DefaultRequestHeaders.Authorization);
    }

    [Fact]
    public void Given_TokenChangedViaSetToken_When_CreateClientIsCalled_Then_ShouldUseUpdatedToken()
    {
        // Arrange
        _authState.SetAuthenticated("Ion Popescu", "ion@example.com", "initial-token");
        _authState.SetToken("updated-token");
        var httpClient = new HttpClient { BaseAddress = new Uri("https://localhost") };
        _httpClientFactoryMock
            .Setup(f => f.CreateClient("API"))
            .Returns(httpClient);

        // Act
        var client = _apiClient.CreateClient();

        // Assert
        Assert.Equal("updated-token", client.DefaultRequestHeaders.Authorization?.Parameter);
    }
}

