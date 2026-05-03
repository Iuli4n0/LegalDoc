using IdentityService.Application.Abstractions;
using IdentityService.Application.Commands.IncrementDocumentCount;
using IdentityService.Domain.Entities;
using Moq;

namespace IdentityService.Test.Commands;

public class IncrementDocumentCountCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly IncrementDocumentCountCommandHandler _handler;

    public IncrementDocumentCountCommandHandlerTests()
    {
        _handler = new IncrementDocumentCountCommandHandler(_userRepo.Object);
    }

    [Fact]
    public async Task UserNotFound_Throws()
    {
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _handler.Handle(new IncrementDocumentCountCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Valid_IncrementsAndReturns()
    {
        var user = User.Create("u@t.com", "h", "N");
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(user);
        _userRepo.Setup(r => r.UpdateAsync(user)).Returns(Task.CompletedTask);

        var result = await _handler.Handle(new IncrementDocumentCountCommand(user.Id), CancellationToken.None);

        Assert.Equal(1, result.TotalDocumentsUploaded);
        Assert.Equal(1, result.MaxDocuments);
        _userRepo.Verify(r => r.UpdateAsync(user), Times.Once);
    }
}
