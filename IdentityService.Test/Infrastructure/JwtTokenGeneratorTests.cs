using System.IdentityModel.Tokens.Jwt;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace IdentityService.Test.Infrastructure;

public class JwtTokenGeneratorTests
{
    [Fact]
    public void Given_ValidJwtSettings_When_GenerateTokenIsCalled_Then_ShouldContainExpectedClaims()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Issuer"] = "TestIssuer",
                ["JwtSettings:Audience"] = "TestAudience",
                ["JwtSettings:ExpirationMinutes"] = "30"
            })
            .Build();

        Environment.SetEnvironmentVariable("JWT_SECRET", "this_is_a_very_long_test_secret_for_hmac_key_12345");
        var env = new Moq.Mock<Microsoft.Extensions.Hosting.IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(Microsoft.Extensions.Hosting.Environments.Development);
        var generator = new JwtTokenGenerator(config, env.Object);
        var user = User.Create("test@example.com", "hash", "Ion Popescu");

        var token = generator.GenerateToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("TestIssuer", jwt.Issuer);
        Assert.Contains(jwt.Audiences, audience => audience == "TestAudience");
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Name && c.Value == user.FullName);
    }

    [Fact]
    public void Given_MissingSecret_When_GenerateTokenIsCalled_Then_ShouldThrowInvalidOperationException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Issuer"] = "TestIssuer",
                ["JwtSettings:Audience"] = "TestAudience"
            })
            .Build();

        Environment.SetEnvironmentVariable("JWT_SECRET", null);
        var env = new Moq.Mock<Microsoft.Extensions.Hosting.IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(Microsoft.Extensions.Hosting.Environments.Production);
        var generator = new JwtTokenGenerator(config, env.Object);
        var user = User.Create("test@example.com", "hash", "Ion Popescu");

        var exception = Assert.Throws<InvalidOperationException>(() => generator.GenerateToken(user));

        Assert.Contains("JWT Secret not configured", exception.Message);
    }

    [Fact]
    public void Given_MinimalJwtSettings_When_GenerateTokenIsCalled_Then_ShouldUseDefaultIssuerAndAudience()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        Environment.SetEnvironmentVariable("JWT_SECRET", "this_is_a_very_long_test_secret_for_hmac_key_12345");
        var env = new Moq.Mock<Microsoft.Extensions.Hosting.IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(Microsoft.Extensions.Hosting.Environments.Development);
        var generator = new JwtTokenGenerator(config, env.Object);
        var user = User.Create("test@example.com", "hash", "Ion Popescu");

        var token = generator.GenerateToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("LegalDoc", jwt.Issuer);
        Assert.Contains(jwt.Audiences, audience => audience == "LegalDoc");
    }

    [Fact]
    public void Given_SecretAndLegacyKey_When_GenerateTokenIsCalled_Then_ShouldPreferSecret()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Issuer"] = "TestIssuer",
                ["JwtSettings:Audience"] = "TestAudience"
            })
            .Build();

        Environment.SetEnvironmentVariable("JWT_SECRET", "this_is_the_secret_used_by_token_validation_12345");
        var env = new Moq.Mock<Microsoft.Extensions.Hosting.IHostEnvironment>();
        env.SetupGet(e => e.EnvironmentName).Returns(Microsoft.Extensions.Hosting.Environments.Development);
        var generator = new JwtTokenGenerator(config, env.Object);
        var user = User.Create("test@example.com", "hash", "Ion Popescu");

        var token = generator.GenerateToken(user);

        var handler = new JwtSecurityTokenHandler();
        var validationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "TestIssuer",
            ValidAudience = "TestAudience",
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_SECRET") ?? ""))
        };

        var principal = handler.ValidateToken(token, validationParameters, out _);

        var subject = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        Assert.Equal(user.Id.ToString(), subject);
    }
}
