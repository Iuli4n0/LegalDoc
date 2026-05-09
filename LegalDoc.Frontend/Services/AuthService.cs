using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using LegalDoc.Frontend.Models;

namespace LegalDoc.Frontend.Services;

internal class AuthService
{
    private const string AuthTokenKey = "authToken";
    private const string UserNameKey = "userName";
    private const string UserEmailKey = "userEmail";
    private const string UserRoleKey = "userRole";

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
        var response = await client.PostAsJsonAsync("/api/auth/login", request).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("Email sau parolă incorectă.");

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>().ConfigureAwait(false);
        if (result is null)
            throw new InvalidOperationException("Răspuns invalid de la server.");

        var role = ExtractRoleFromToken(result.Token);

        await _authStorage.SetAsync(AuthTokenKey, result.Token).ConfigureAwait(false);
        await _authStorage.SetAsync(UserNameKey, result.FullName).ConfigureAwait(false);
        await _authStorage.SetAsync(UserEmailKey, result.Email).ConfigureAwait(false);
        await _authStorage.SetAsync(UserRoleKey, role).ConfigureAwait(false);

        _authState.SetAuthenticated(result.FullName, result.Email, result.Token, role);

        return result;
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        var client = _httpClientFactory.CreateClient("IdentityAPI");
        var response = await client.PostAsJsonAsync("/api/auth/register", request).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException("Un cont cu acest email există deja.");
        }

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<RegisterResponse>().ConfigureAwait(false);
        if (result is null)
            throw new InvalidOperationException("Răspuns invalid de la server.");

        return result;
    }

    public async Task LogoutAsync()
    {
        await _authStorage.DeleteAsync(AuthTokenKey).ConfigureAwait(false);
        await _authStorage.DeleteAsync(UserNameKey).ConfigureAwait(false);
        await _authStorage.DeleteAsync(UserEmailKey).ConfigureAwait(false);
        await _authStorage.DeleteAsync(UserRoleKey).ConfigureAwait(false);
        _authState.SetLoggedOut();
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            var result = await _authStorage.GetAsync(AuthTokenKey).ConfigureAwait(false);
            return result.Success ? result.Value : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync().ConfigureAwait(false);
        return !string.IsNullOrEmpty(token);
    }

    public async Task InitializeAsync()
    {
        try
        {
            var tokenResult = await _authStorage.GetAsync(AuthTokenKey).ConfigureAwait(false);
            var nameResult = await _authStorage.GetAsync(UserNameKey).ConfigureAwait(false);
            var emailResult = await _authStorage.GetAsync(UserEmailKey).ConfigureAwait(false);
            var roleResult = await _authStorage.GetAsync(UserRoleKey).ConfigureAwait(false);

            if (tokenResult.Success && !string.IsNullOrEmpty(tokenResult.Value))
            {
                var role = roleResult.Success ? roleResult.Value ?? "User" : "User";
                _authState.SetAuthenticated(
                    nameResult.Success ? nameResult.Value ?? "" : "",
                    emailResult.Success ? emailResult.Value ?? "" : "",
                    tokenResult.Value,
                    role);
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

    private static string ExtractRoleFromToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);
            var roleClaim = jwt.Claims.FirstOrDefault(c =>
                c.Type == "role" ||
                c.Type == System.Security.Claims.ClaimTypes.Role ||
                c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role");
            return roleClaim?.Value ?? "User";
        }
        catch
        {
            return "User";
        }
    }
}
