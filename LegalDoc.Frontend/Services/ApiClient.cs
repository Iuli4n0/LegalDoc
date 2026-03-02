using System.Net.Http.Headers;

namespace LegalDoc.Frontend.Services;

/// <summary>
/// Scoped service that creates an HttpClient with the JWT token from the current Blazor circuit.
/// This solves the problem where IHttpClientFactory's DelegatingHandler runs in a separate DI scope
/// and cannot access the Blazor component's scoped AuthStateService.
/// </summary>
public class ApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthStateService _authState;

    public ApiClient(IHttpClientFactory httpClientFactory, AuthStateService authState)
    {
        _httpClientFactory = httpClientFactory;
        _authState = authState;
    }

    public HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("API");

        var token = _authState.Token;
        if (!string.IsNullOrEmpty(token))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }
}

