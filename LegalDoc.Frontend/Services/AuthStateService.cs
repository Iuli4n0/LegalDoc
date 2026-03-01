namespace LegalDoc.Frontend.Services;

public class AuthStateService
{
    public bool IsAuthenticated { get; private set; }
    public string UserName { get; private set; } = string.Empty;
    public string UserEmail { get; private set; } = string.Empty;
    public string? Token { get; private set; }

    public event Action? OnChange;

    public void SetAuthenticated(string userName, string userEmail, string? token = null)
    {
        IsAuthenticated = true;
        UserName = userName;
        UserEmail = userEmail;
        Token = token;
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
        OnChange?.Invoke();
    }
}

