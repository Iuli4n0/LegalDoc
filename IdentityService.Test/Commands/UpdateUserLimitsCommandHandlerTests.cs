using IdentityService.Application.Abstractions;
using IdentityService.Application.Commands.UpdateUserLimits;
using IdentityService.Domain.Entities;
using Moq;

namespace IdentityService.Test.Commands;

public class UpdateUserLimitsCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly UpdateUserLimitsCommandHandler _handler;

    public UpdateUserLimitsCommandHandlerTests()
    {
        _handler = new UpdateUserLimitsCommandHandler(_userRepo.Object);
    }

    [Fact]
    public async Task UserNotFound_Throws()
    {
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _handler.Handle(new UpdateUserLimitsCommand(Guid.NewGuid(), 10, 5), CancellationToken.None));
    }

    [Fact]
    public async Task Valid_UpdatesAndSaves()
    {
        var user = User.Create("u@t.com", "h", "N");
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(user);
        _userRepo.Setup(r => r.UpdateAsync(user)).Returns(Task.CompletedTask);

        await _handler.Handle(new UpdateUserLimitsCommand(user.Id, 10, 5), CancellationToken.None);

        Assert.Equal(10, user.MaxDocuments);
        Assert.Equal(5, user.MaxDocumentSizeMb);
        _userRepo.Verify(r => r.UpdateAsync(user), Times.Once);
    }
}
