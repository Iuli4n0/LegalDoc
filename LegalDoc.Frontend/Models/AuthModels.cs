namespace LegalDoc.Frontend.Models;

internal class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

internal class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

internal record LoginResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Token,
    DateTime ExpiresAt
);

internal record RegisterResponse(
    Guid Id,
    string Email,
    string FullName,
    DateTime CreatedAt
);
