namespace LegalDoc.Frontend.Services;

public class AuthStateService
{
    public bool IsAuthenticated { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string UserEmail { get; private set; } = string.Empty;
    public string? Token { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public bool IsAdmin => string.Equals(Role, "Admin", System.StringComparison.OrdinalIgnoreCase);

    public event Action? OnChange;

    public void SetAuthenticated(string userName, string userEmail, string? token = null, string role = "User")
    {
        IsAuthenticated = true;
        UserName = userName;
        UserEmail = userEmail;
        Token = token;
        Role = role;
        OnChange?.Invoke();
    }

    public void SetToken(string? token)
    {
        Token = token;
    }

    public void SetLoggedOut()
    {
        IsAuthenticated = false;
        UserName = string.Empty;
        UserEmail = string.Empty;
        Token = null;
        Role = string.Empty;
        OnChange?.Invoke();
    }
}
