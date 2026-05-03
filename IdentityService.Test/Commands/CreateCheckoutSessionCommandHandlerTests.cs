using IdentityService.Application.Abstractions;
using IdentityService.Application.Commands.CreateCheckoutSession;
using IdentityService.Domain.Entities;
using Moq;

namespace IdentityService.Test.Commands;

public class CreateCheckoutSessionCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IStripeService> _stripe = new();
    private readonly CreateCheckoutSessionCommandHandler _handler;

    public CreateCheckoutSessionCommandHandlerTests()
    {
        _handler = new CreateCheckoutSessionCommandHandler(_userRepo.Object, _stripe.Object);
    }

    [Fact]
    public async Task UserNotFound_Throws()
    {
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _handler.Handle(new CreateCheckoutSessionCommand(Guid.NewGuid(), SubscriptionPlan.Bronze), CancellationToken.None));
    }

    [Fact]
    public async Task FreePlan_Throws()
    {
        var user = User.Create("u@t.com", "h", "N");
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(user);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(new CreateCheckoutSessionCommand(user.Id, SubscriptionPlan.Free), CancellationToken.None));
    }

    [Fact]
    public async Task Downgrade_Throws()
    {
        var user = User.Create("u@t.com", "h", "N");
        user.UpdateSubscription(SubscriptionPlan.Gold);
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(user);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(new CreateCheckoutSessionCommand(user.Id, SubscriptionPlan.Bronze), CancellationToken.None));
    }

    [Fact]
    public async Task SamePlan_Throws()
    {
        var user = User.Create("u@t.com", "h", "N");
        user.UpdateSubscription(SubscriptionPlan.Bronze);
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(user);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.Handle(new CreateCheckoutSessionCommand(user.Id, SubscriptionPlan.Bronze), CancellationToken.None));
    }

    [Fact]
    public async Task Valid_ReturnsUrl()
    {
        var user = User.Create("u@t.com", "h", "N");
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(user);
        _stripe.Setup(s => s.CreateCheckoutSessionAsync(It.IsAny<Guid>(), It.IsAny<string>(), SubscriptionPlan.Bronze))
            .ReturnsAsync("https://checkout.stripe.com/session");

        var result = await _handler.Handle(new CreateCheckoutSessionCommand(user.Id, SubscriptionPlan.Bronze), CancellationToken.None);
        Assert.Equal("https://checkout.stripe.com/session", result.CheckoutUrl);
    }
}
