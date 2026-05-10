using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Infrastructure.Services;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private const int DefaultExpirationMinutes = 1440; // 24 hours
    private const string DefaultIssuer = "LegalDoc";
    private const string DefaultAudience = "LegalDoc";

    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration, IHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(env);

        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        // Prefer environment variable but fall back to configuration (JwtSettings:Secret)
        var keyStr = Environment.GetEnvironmentVariable("JWT_SECRET")
                     ?? _configuration["JwtSettings:Secret"];

        if (string.IsNullOrWhiteSpace(keyStr))
        {
            throw new InvalidOperationException("JWT Secret not configured. Set JwtSettings:Secret or JWT_SECRET environment variable.");
        }

        var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
            ?? _configuration["JwtSettings:Issuer"]
            ?? DefaultIssuer;
        var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
            ?? _configuration["JwtSettings:Audience"]
            ?? DefaultAudience;
        var expirationRaw = Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES")
            ?? _configuration["JwtSettings:ExpirationMinutes"]
            ?? DefaultExpirationMinutes.ToString();

        // Be tolerant of invalid/missing expiration values
        if (!int.TryParse(expirationRaw, out var expirationMinutes) || expirationMinutes <= 0)
        {
            expirationMinutes = DefaultExpirationMinutes;
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyStr));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name, user.FullName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
