using IdentityService.Application.Abstractions;

namespace IdentityService.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;
    
    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: WorkFactor);
    }

    public bool Verify(string password, string passwordHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}

