using System.Net.Http.Json;
using LegalDoc.Frontend.Models;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace LegalDoc.Frontend.Services;

public class AuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ProtectedLocalStorage _localStorage;
    private readonly AuthStateService _authState;

    public AuthService(
        IHttpClientFactory httpClientFactory,
        ProtectedLocalStorage localStorage,
        AuthStateService authState)
    {
        _httpClientFactory = httpClientFactory;
        _localStorage = localStorage;
        _authState = authState;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var client = _httpClientFactory.CreateClient("IdentityAPI");
        var response = await client.PostAsJsonAsync("/api/auth/login", request);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("Email sau parolă incorectă.");

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>()
                     ?? throw new Exception("Răspuns invalid de la server.");

        await _localStorage.SetAsync("authToken", result.Token);
        await _localStorage.SetAsync("userName", result.FullName);
        await _localStorage.SetAsync("userEmail", result.Email);

        _authState.SetAuthenticated(result.FullName, result.Email, result.Token);

        return result;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        var client = _httpClientFactory.CreateClient("IdentityAPI");
        var response = await client.PostAsJsonAsync("/api/auth/register", request);

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException("Un cont cu acest email există deja.");
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RegisterResponse>()
               ?? throw new Exception("Răspuns invalid de la server.");
    }

    public async Task LogoutAsync()
    {
        await _localStorage.DeleteAsync("authToken");
        await _localStorage.DeleteAsync("userName");
        await _localStorage.DeleteAsync("userEmail");
        _authState.SetLoggedOut();
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            var result = await _localStorage.GetAsync<string>("authToken");
            return result.Success ? result.Value : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    public async Task InitializeAsync()
    {
        try
        {
            var tokenResult = await _localStorage.GetAsync<string>("authToken");
            var nameResult = await _localStorage.GetAsync<string>("userName");
            var emailResult = await _localStorage.GetAsync<string>("userEmail");

            if (tokenResult.Success && !string.IsNullOrEmpty(tokenResult.Value))
            {
                _authState.SetAuthenticated(
                    nameResult.Success ? nameResult.Value ?? "" : "",
                    emailResult.Success ? emailResult.Value ?? "" : "",
                    tokenResult.Value);
            }
            else
            {
                _authState.SetLoggedOut();
            }
        }
        catch
        {
            _authState.SetLoggedOut();
        }
    }
}

