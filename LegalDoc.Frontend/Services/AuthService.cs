using System.Net.Http.Json;
using LegalDoc.Frontend.Models;

namespace LegalDoc.Frontend.Services;

public class AuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthStorage _authStorage;
    private readonly AuthStateService _authState;

    public AuthService(
        IHttpClientFactory httpClientFactory,
        IAuthStorage authStorage,
        AuthStateService authState)
    {
        _httpClientFactory = httpClientFactory;
        _authStorage = authStorage;
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

        await _authStorage.SetAsync("authToken", result.Token);
        await _authStorage.SetAsync("userName", result.FullName);
        await _authStorage.SetAsync("userEmail", result.Email);

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
        await _authStorage.DeleteAsync("authToken");
        await _authStorage.DeleteAsync("userName");
        await _authStorage.DeleteAsync("userEmail");
        _authState.SetLoggedOut();
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            var result = await _authStorage.GetAsync("authToken");
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
            var tokenResult = await _authStorage.GetAsync("authToken");
            var nameResult = await _authStorage.GetAsync("userName");
            var emailResult = await _authStorage.GetAsync("userEmail");

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
