using IdentityService.Infrastructure.Services;

namespace IdentityService.Test.Infrastructure;

public class PasswordHasherTests
{
    private readonly PasswordHasher _passwordHasher = new();

    [Fact]
    public void Given_PlainPassword_When_HashIsCalled_Then_ShouldReturnDifferentHash()
    {
        var password = "MyStrongPassword!123";

        var hash = _passwordHasher.Hash(password);

        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.NotEqual(password, hash);
    }

    [Fact]
    public void Given_ValidPasswordAndHash_When_VerifyIsCalled_Then_ShouldReturnTrue()
    {
        var password = "MyStrongPassword!123";
        var hash = _passwordHasher.Hash(password);

        var result = _passwordHasher.Verify(password, hash);

        Assert.True(result);
    }

    [Fact]
    public void Given_InvalidPasswordAndHash_When_VerifyIsCalled_Then_ShouldReturnFalse()
    {
        var hash = _passwordHasher.Hash("correct-password");

        var result = _passwordHasher.Verify("wrong-password", hash);

        Assert.False(result);
    }
}
