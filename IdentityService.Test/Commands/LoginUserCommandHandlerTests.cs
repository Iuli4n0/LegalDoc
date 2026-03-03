using IdentityService.Application.Abstractions;
using IdentityService.Application.Commands.LoginUser;
using IdentityService.Domain.Entities;
using Moq;

namespace IdentityService.Test.Commands;

public class LoginUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGeneratorMock;
    private readonly LoginUserCommandHandler _handler;

    public LoginUserCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _jwtTokenGeneratorMock = new Mock<IJwtTokenGenerator>();
        _handler = new LoginUserCommandHandler(
            _userRepositoryMock.Object,
            _passwordHasherMock.Object,
            _jwtTokenGeneratorMock.Object);
    }

    [Fact]
    public async Task Given_ValidCredentials_When_HandleIsCalled_Then_ShouldReturnLoginResponse()
    {
        // Arrange
        var command = new LoginUserCommand
        {
            Email = "test@example.com",
            Password = "SecurePass123!"
        };

        var user = User.Create("test@example.com", "hashed_password", "Ion Popescu");

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(command.Email))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(h => h.Verify(command.Password, user.PasswordHash))
            .Returns(true);

        _userRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        _jwtTokenGeneratorMock
            .Setup(g => g.GenerateToken(user))
            .Returns("jwt_token_123");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("test@example.com", result.Email);
        Assert.Equal("Ion Popescu", result.FullName);
        Assert.Equal("jwt_token_123", result.Token);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Given_ValidCredentials_When_HandleIsCalled_Then_ShouldGenerateJwtToken()
    {
        // Arrange
        var command = new LoginUserCommand
        {
            Email = "test@example.com",
            Password = "SecurePass123!"
        };

        var user = User.Create("test@example.com", "hashed_password", "Ion Popescu");

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(command.Email))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(h => h.Verify(command.Password, It.IsAny<string>()))
            .Returns(true);

        _userRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        _jwtTokenGeneratorMock
            .Setup(g => g.GenerateToken(user))
            .Returns("jwt_token_123");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _jwtTokenGeneratorMock.Verify(g => g.GenerateToken(user), Times.Once);
    }

    [Fact]
    public async Task Given_ValidCredentials_When_HandleIsCalled_Then_ShouldUpdateLastLogin()
    {
        // Arrange
        var command = new LoginUserCommand
        {
            Email = "test@example.com",
            Password = "SecurePass123!"
        };

        var user = User.Create("test@example.com", "hashed_password", "Ion Popescu");

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(command.Email))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(h => h.Verify(command.Password, It.IsAny<string>()))
            .Returns(true);

        _userRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        _jwtTokenGeneratorMock
            .Setup(g => g.GenerateToken(It.IsAny<User>()))
            .Returns("token");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _userRepositoryMock.Verify(r => r.UpdateAsync(It.Is<User>(u =>
            u.LastLoginAt != null
        )), Times.Once);
    }

    [Fact]
    public async Task Given_NonExistentEmail_When_HandleIsCalled_Then_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var command = new LoginUserCommand
        {
            Email = "nonexistent@example.com",
            Password = "SecurePass123!"
        };

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(command.Email))
            .ReturnsAsync((User?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("Invalid email or password", exception.Message);
    }

    [Fact]
    public async Task Given_WrongPassword_When_HandleIsCalled_Then_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        var command = new LoginUserCommand
        {
            Email = "test@example.com",
            Password = "WrongPassword!"
        };

        var user = User.Create("test@example.com", "hashed_password", "Ion Popescu");

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(command.Email))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(h => h.Verify(command.Password, user.PasswordHash))
            .Returns(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("Invalid email or password", exception.Message);
    }

    [Fact]
    public async Task Given_WrongPassword_When_HandleIsCalled_Then_ShouldNotGenerateTokenOrUpdateUser()
    {
        // Arrange
        var command = new LoginUserCommand
        {
            Email = "test@example.com",
            Password = "WrongPassword!"
        };

        var user = User.Create("test@example.com", "hashed_password", "Ion Popescu");

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(command.Email))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(h => h.Verify(command.Password, user.PasswordHash))
            .Returns(false);

        // Act
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _handler.Handle(command, CancellationToken.None));

        // Assert
        _jwtTokenGeneratorMock.Verify(g => g.GenerateToken(It.IsAny<User>()), Times.Never);
        _userRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Given_NonExistentEmail_When_HandleIsCalled_Then_ShouldNotVerifyPasswordOrGenerateToken()
    {
        // Arrange
        var command = new LoginUserCommand
        {
            Email = "nonexistent@example.com",
            Password = "SecurePass123!"
        };

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(command.Email))
            .ReturnsAsync((User?)null);

        // Act
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _handler.Handle(command, CancellationToken.None));

        // Assert
        _passwordHasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _jwtTokenGeneratorMock.Verify(g => g.GenerateToken(It.IsAny<User>()), Times.Never);
        _userRepositoryMock.Verify(r => r.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Given_ValidCredentials_When_HandleIsCalled_Then_ShouldVerifyPasswordWithCorrectArguments()
    {
        // Arrange
        var command = new LoginUserCommand
        {
            Email = "test@example.com",
            Password = "SecurePass123!"
        };

        var user = User.Create("test@example.com", "hashed_password", "Ion Popescu");

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(command.Email))
            .ReturnsAsync(user);

        _passwordHasherMock
            .Setup(h => h.Verify(command.Password, user.PasswordHash))
            .Returns(true);

        _userRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        _jwtTokenGeneratorMock
            .Setup(g => g.GenerateToken(It.IsAny<User>()))
            .Returns("token");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _passwordHasherMock.Verify(h => h.Verify("SecurePass123!", "hashed_password"), Times.Once);
    }
}

