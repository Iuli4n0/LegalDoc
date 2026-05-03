using IdentityService.Application.Abstractions;
using IdentityService.Application.Queries.CheckUserLimits;
using IdentityService.Domain.Entities;
using Moq;

namespace IdentityService.Test.Queries;

public class CheckUserLimitsQueryHandlerTests
{
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly CheckUserLimitsQueryHandler _handler;

    public CheckUserLimitsQueryHandlerTests()
    {
        _handler = new CheckUserLimitsQueryHandler(_userRepo.Object);
    }

    [Fact]
    public async Task UserNotFound_Throws()
    {
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _handler.Handle(new CheckUserLimitsQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task Valid_ReturnsLimits()
    {
        var user = User.Create("u@t.com", "h", "N");
        _userRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(user);
        _userRepo.Setup(r => r.UpdateAsync(user)).Returns(Task.CompletedTask);

        var result = await _handler.Handle(new CheckUserLimitsQuery(user.Id), CancellationToken.None);

        Assert.Equal(0, result.TotalDocumentsUploaded);
        Assert.Equal(1, result.MaxDocuments);
        Assert.True(result.CanUpload);
        Assert.Equal("Free", result.SubscriptionPlan);
        Assert.Equal(1, result.RemainingUploads);
    }
}
