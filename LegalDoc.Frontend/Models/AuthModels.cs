namespace LegalDoc.Frontend.Models;

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public record LoginResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Token,
    DateTime ExpiresAt
);

public record RegisterResponse(
    Guid Id,
    string Email,
    string FullName,
    DateTime CreatedAt
);

