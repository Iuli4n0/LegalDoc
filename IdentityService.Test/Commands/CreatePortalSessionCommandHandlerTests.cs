using IdentityService.Application.Abstractions;
using IdentityService.Application.Commands.CreatePortalSession;
using IdentityService.Domain.Entities;
using Moq;

namespace IdentityService.Test.Commands;

public class CreatePortalSessionCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IStripeService> _stripe = new();
    private readonly CreatePortalSessionCommandHandler _handler;

    public CreatePortalSessionCommandHandlerTests()
    {
        _handler = new CreatePortalSessionCommandHandler(_userRepo.Object, _stripe.Object);
    }

    [Fact]
    public async Task UserNotFound_Throws()
    {
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _handler.Handle(new CreatePortalSessionCommand(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task NoStripeCustomerId_Throws()
    {
        var user = User.Create("u@t.com", "h", "N");
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(user);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(new CreatePortalSessionCommand(user.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Valid_ReturnsUrl()
    {
        var user = User.Create("u@t.com", "h", "N");
        user.SetStripeCustomerId("cus_123");
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(user);
        _stripe.Setup(s => s.CreateCustomerPortalSessionAsync("cus_123")).ReturnsAsync("https://portal.url");

        var result = await _handler.Handle(new CreatePortalSessionCommand(user.Id), CancellationToken.None);
        Assert.Equal("https://portal.url", result.PortalUrl);
    }
}
