using IdentityService.Application.Abstractions;
using IdentityService.Application.Queries.GetAllUsers;
using IdentityService.Domain.Entities;
using Moq;

namespace IdentityService.Test.Queries;

public class GetAllUsersQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly GetAllUsersQueryHandler _handler;

    public GetAllUsersQueryHandlerTests()
    {
        _handler = new GetAllUsersQueryHandler(_userRepo.Object);
    }

    [Fact]
    public async Task ReturnsAllUsers()
    {
        var u1 = User.Create("a@t.com", "h", "A");
        var u2 = User.Create("b@t.com", "h", "B");
        _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new[] { u1, u2 });

        var result = await _handler.Handle(new GetAllUsersQuery(), CancellationToken.None);

        Assert.Equal(2, result.Users.Count());
    }

    [Fact]
    public async Task NoUsers_ReturnsEmpty()
    {
        _userRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(Enumerable.Empty<User>());
        var result = await _handler.Handle(new GetAllUsersQuery(), CancellationToken.None);
        Assert.Empty(result.Users);
    }
}
