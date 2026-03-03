using IdentityService.Application.Abstractions;
using IdentityService.Application.Commands.RegisterUser;
using IdentityService.Domain.Entities;
using Moq;

namespace IdentityService.Test.Commands;

public class RegisterUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly RegisterUserCommandHandler _handler;

    public RegisterUserCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _handler = new RegisterUserCommandHandler(
            _userRepositoryMock.Object,
            _passwordHasherMock.Object);
    }

    [Fact]
    public async Task Given_ValidCommand_When_HandleIsCalled_Then_ShouldReturnRegisterUserResponse()
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            Email = "test@example.com",
            Password = "SecurePass123!",
            FullName = "Ion Popescu"
        };

        _userRepositoryMock
            .Setup(r => r.ExistsAsync(command.Email))
            .ReturnsAsync(false);

        _passwordHasherMock
            .Setup(h => h.Hash(command.Password))
            .Returns("hashed_password");

        _userRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("test@example.com", result.Email);
        Assert.Equal("Ion Popescu", result.FullName);
        Assert.True(result.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task Given_ValidCommand_When_HandleIsCalled_Then_ShouldHashPassword()
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            Email = "test@example.com",
            Password = "SecurePass123!",
            FullName = "Ion Popescu"
        };

        _userRepositoryMock
            .Setup(r => r.ExistsAsync(command.Email))
            .ReturnsAsync(false);

        _passwordHasherMock
            .Setup(h => h.Hash(command.Password))
            .Returns("hashed_password");

        _userRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _passwordHasherMock.Verify(h => h.Hash("SecurePass123!"), Times.Once);
    }

    [Fact]
    public async Task Given_ValidCommand_When_HandleIsCalled_Then_ShouldSaveUserToRepository()
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            Email = "test@example.com",
            Password = "SecurePass123!",
            FullName = "Ion Popescu"
        };

        _userRepositoryMock
            .Setup(r => r.ExistsAsync(command.Email))
            .ReturnsAsync(false);

        _passwordHasherMock
            .Setup(h => h.Hash(command.Password))
            .Returns("hashed_password");

        _userRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _userRepositoryMock.Verify(r => r.AddAsync(It.Is<User>(u =>
            u.Email == "test@example.com" &&
            u.FullName == "Ion Popescu" &&
            u.PasswordHash == "hashed_password"
        )), Times.Once);
    }

    [Fact]
    public async Task Given_ExistingEmail_When_HandleIsCalled_Then_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            Email = "existing@example.com",
            Password = "SecurePass123!",
            FullName = "Ion Popescu"
        };

        _userRepositoryMock
            .Setup(r => r.ExistsAsync(command.Email))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        Assert.Contains("already exists", exception.Message);
    }

    [Fact]
    public async Task Given_ExistingEmail_When_HandleIsCalled_Then_ShouldNotHashPasswordOrSaveUser()
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            Email = "existing@example.com",
            Password = "SecurePass123!",
            FullName = "Ion Popescu"
        };

        _userRepositoryMock
            .Setup(r => r.ExistsAsync(command.Email))
            .ReturnsAsync(true);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, CancellationToken.None));

        // Assert
        _passwordHasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        _userRepositoryMock.Verify(r => r.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Given_ValidCommand_When_HandleIsCalled_Then_ShouldCheckIfUserExists()
    {
        // Arrange
        var command = new RegisterUserCommand
        {
            Email = "test@example.com",
            Password = "SecurePass123!",
            FullName = "Ion Popescu"
        };

        _userRepositoryMock
            .Setup(r => r.ExistsAsync(command.Email))
            .ReturnsAsync(false);

        _passwordHasherMock
            .Setup(h => h.Hash(It.IsAny<string>()))
            .Returns("hashed");

        _userRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<User>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _userRepositoryMock.Verify(r => r.ExistsAsync("test@example.com"), Times.Once);
    }
}

