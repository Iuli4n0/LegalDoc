using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace LegalDoc.Frontend.Services;

public interface IAuthStorage
{
    Task SetAsync(string key, string value);
    Task DeleteAsync(string key);
    Task<AuthStorageValue> GetAsync(string key);
}

public readonly record struct AuthStorageValue(bool Success, string? Value);

public class ProtectedLocalAuthStorage : IAuthStorage
{
    private readonly ProtectedLocalStorage _localStorage;

    public ProtectedLocalAuthStorage(ProtectedLocalStorage localStorage)
    {
        _localStorage = localStorage;
    }

    public Task SetAsync(string key, string value) => _localStorage.SetAsync(key, value).AsTask();

    public Task DeleteAsync(string key) => _localStorage.DeleteAsync(key).AsTask();

    public async Task<AuthStorageValue> GetAsync(string key)
    {
        var result = await _localStorage.GetAsync<string>(key);
        return new AuthStorageValue(result.Success, result.Value);
    }
}
