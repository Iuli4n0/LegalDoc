using IdentityService.Application.Abstractions;
using IdentityService.Application.Queries.GetUserById;
using IdentityService.Domain.Entities;
using Moq;

namespace IdentityService.Test.Queries;

public class GetUserByIdQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly GetUserByIdQueryHandler _handler;

    public GetUserByIdQueryHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _handler = new GetUserByIdQueryHandler(_userRepositoryMock.Object);
    }

    [Fact]
    public async Task Given_ExistingUserId_When_HandleIsCalled_Then_ShouldReturnUserResponse()
    {
        // Arrange
        var user = User.Create("test@example.com", "hashed_password", "Ion Popescu");
        var query = new GetUserByIdQuery(user.Id);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.Id))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
        Assert.Equal("test@example.com", result.Email);
        Assert.Equal("Ion Popescu", result.FullName);
        Assert.Equal(user.CreatedAt, result.CreatedAt);
        Assert.Null(result.LastLoginAt);
    }

    [Fact]
    public async Task Given_NonExistentUserId_When_HandleIsCalled_Then_ShouldReturnNull()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var query = new GetUserByIdQuery(nonExistentId);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(nonExistentId))
            .ReturnsAsync((User?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Given_ExistingUserId_When_HandleIsCalled_Then_ShouldCallRepositoryWithCorrectId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserByIdQuery(userId);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync((User?)null);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _userRepositoryMock.Verify(r => r.GetByIdAsync(userId), Times.Once);
    }

    [Fact]
    public async Task Given_UserWithLastLogin_When_HandleIsCalled_Then_ShouldReturnResponseWithLastLoginAt()
    {
        // Arrange
        var user = User.Create("test@example.com", "hashed_password", "Ion Popescu");
        user.UpdateLastLogin();
        var query = new GetUserByIdQuery(user.Id);

        _userRepositoryMock
            .Setup(r => r.GetByIdAsync(user.Id))
            .ReturnsAsync(user);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.LastLoginAt);
    }
}

