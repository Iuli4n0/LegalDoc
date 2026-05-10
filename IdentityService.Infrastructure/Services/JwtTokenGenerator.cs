using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace IdentityService.Infrastructure.Services;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private const int DefaultExpirationMinutes = 1440; // 24 hours
    private const string DefaultIssuer = "LegalDoc";
    private const string DefaultAudience = "LegalDoc";

    private readonly IConfiguration _configuration;

    public JwtTokenGenerator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var keyStr = Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? jwtSettings["Secret"]
            ?? jwtSettings["Key"]
            ?? throw new InvalidOperationException("JWT Secret not configured");
        var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
            ?? jwtSettings["Issuer"]
            ?? DefaultIssuer;
        var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
            ?? jwtSettings["Audience"]
            ?? DefaultAudience;
        var expirationRaw = Environment.GetEnvironmentVariable("JWT_EXPIRATION_MINUTES")
            ?? jwtSettings["ExpirationMinutes"]
            ?? DefaultExpirationMinutes.ToString();
        var expirationMinutes = int.Parse(expirationRaw);

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
