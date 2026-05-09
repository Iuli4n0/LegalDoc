using System.Security.Claims;
using IdentityService.API.Controllers;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Commands.CreateCheckoutSession;
using IdentityService.Application.Commands.CreatePortalSession;
using IdentityService.Application.Commands.IncrementDocumentCount;
using IdentityService.Application.Commands.UpdateUserLimits;
using IdentityService.Application.Queries.CheckUserLimits;
using IdentityService.Application.Queries.GetAllUsers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace IdentityService.Test.API;

public class AuthControllerExtraTests
{
    private readonly Mock<IMediator> _med = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly AuthController _ctrl;

    public AuthControllerExtraTests()
    {
        _ctrl = new AuthController(_med.Object, _userRepo.Object);
    }

    private void SetClaims(params Claim[] claims)
    {
        _ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };
    }

    private void SetNoAuth()
    {
        _ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };
    }

    // ── CreateCheckoutSession ──
    [Fact]
    public async Task Checkout_NoAuth_Unauthorized()
    {
        SetNoAuth();
        var r = await _ctrl.CreateCheckoutSession(new CreateCheckoutRequest("Bronze"));
        Assert.IsType<UnauthorizedResult>(r.Result);
    }

    [Fact]
    public async Task Checkout_InvalidGuid_Unauthorized()
    {
        SetClaims(new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));
        var r = await _ctrl.CreateCheckoutSession(new CreateCheckoutRequest("Bronze"));
        Assert.IsType<UnauthorizedResult>(r.Result);
    }

    [Fact]
    public async Task Checkout_FreePlan_BadRequest()
    {
        SetClaims(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        var r = await _ctrl.CreateCheckoutSession(new CreateCheckoutRequest("Free"));
        Assert.IsType<BadRequestObjectResult>(r.Result);
    }

    [Fact]
    public async Task Checkout_InvalidPlan_BadRequest()
    {
        SetClaims(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        var r = await _ctrl.CreateCheckoutSession(new CreateCheckoutRequest("InvalidPlan"));
        Assert.IsType<BadRequestObjectResult>(r.Result);
    }

    [Fact]
    public async Task Checkout_Valid_Ok()
    {
        var uid = Guid.NewGuid();
        SetClaims(new Claim(ClaimTypes.NameIdentifier, uid.ToString()));
        _med.Setup(m => m.Send(It.IsAny<CreateCheckoutSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateCheckoutSessionResponse("https://url"));
        var r = await _ctrl.CreateCheckoutSession(new CreateCheckoutRequest("Bronze"));
        Assert.IsType<OkObjectResult>(r.Result);
    }

    [Fact]
    public async Task Checkout_KeyNotFound_NotFound()
    {
        var uid = Guid.NewGuid();
        SetClaims(new Claim(ClaimTypes.NameIdentifier, uid.ToString()));
        _med.Setup(m => m.Send(It.IsAny<CreateCheckoutSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("nf"));
        var r = await _ctrl.CreateCheckoutSession(new CreateCheckoutRequest("Bronze"));
        Assert.IsType<NotFoundObjectResult>(r.Result);
    }

    [Fact]
    public async Task Checkout_ArgException_BadReq()
    {
        var uid = Guid.NewGuid();
        SetClaims(new Claim(ClaimTypes.NameIdentifier, uid.ToString()));
        _med.Setup(m => m.Send(It.IsAny<CreateCheckoutSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("e"));
        var r = await _ctrl.CreateCheckoutSession(new CreateCheckoutRequest("Bronze"));
        Assert.IsType<BadRequestObjectResult>(r.Result);
    }

    [Fact]
    public async Task Checkout_500()
    {
        var uid = Guid.NewGuid();
        SetClaims(new Claim(ClaimTypes.NameIdentifier, uid.ToString()));
        _med.Setup(m => m.Send(It.IsAny<CreateCheckoutSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("e"));
        var r = await _ctrl.CreateCheckoutSession(new CreateCheckoutRequest("Bronze"));
        var obj = Assert.IsType<ObjectResult>(r.Result);
        Assert.Equal(500, obj.StatusCode);
    }

    // ── CreatePortalSession ──
    [Fact]
    public async Task Portal_NoAuth_Unauthorized()
    {
        SetNoAuth();
        var r = await _ctrl.CreatePortalSession();
        Assert.IsType<UnauthorizedResult>(r.Result);
    }

    [Fact]
    public async Task Portal_Valid_Ok()
    {
        SetClaims(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        _med.Setup(m => m.Send(It.IsAny<CreatePortalSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreatePortalSessionResponse("https://portal"));
        Assert.IsType<OkObjectResult>((await _ctrl.CreatePortalSession()).Result);
    }

    [Fact]
    public async Task Portal_KeyNotFound() { SetClaims(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())); _med.Setup(m => m.Send(It.IsAny<CreatePortalSessionCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException("nf")); Assert.IsType<NotFoundObjectResult>((await _ctrl.CreatePortalSession()).Result); }
    [Fact]
    public async Task Portal_InvalidOp() { SetClaims(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())); _med.Setup(m => m.Send(It.IsAny<CreatePortalSessionCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("e")); Assert.IsType<BadRequestObjectResult>((await _ctrl.CreatePortalSession()).Result); }
    [Fact]
    public async Task Portal_500() { SetClaims(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())); _med.Setup(m => m.Send(It.IsAny<CreatePortalSessionCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("e")); var r = Assert.IsType<ObjectResult>((await _ctrl.CreatePortalSession()).Result); Assert.Equal(500, r.StatusCode); }

    // ── GetAllUsers ──
    [Fact]
    public async Task GetAll_Ok()
    {
        _med.Setup(m => m.Send(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetAllUsersResponse([]));
        Assert.IsType<OkObjectResult>((await _ctrl.GetAllUsers()).Result);
    }

    [Fact]
    public async Task GetAll_500()
    {
        _med.Setup(m => m.Send(It.IsAny<GetAllUsersQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("e"));
        var r = Assert.IsType<ObjectResult>((await _ctrl.GetAllUsers()).Result);
        Assert.Equal(500, r.StatusCode);
    }

    // ── UpdateUserLimits ──
    [Fact]
    public async Task UpdateLimits_Ok()
    {
        _med.Setup(m => m.Send(It.IsAny<UpdateUserLimitsCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Unit.Value));
        Assert.IsType<NoContentResult>(await _ctrl.UpdateUserLimits(Guid.NewGuid(), new UpdateUserLimitsRequest(10, 5)));
    }

    [Fact] public async Task UpdateLimits_KeyNotFound() { _med.Setup(m => m.Send(It.IsAny<UpdateUserLimitsCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException("nf")); Assert.IsType<NotFoundObjectResult>(await _ctrl.UpdateUserLimits(Guid.NewGuid(), new UpdateUserLimitsRequest(10, 5))); }
    [Fact] public async Task UpdateLimits_ArgEx() { _med.Setup(m => m.Send(It.IsAny<UpdateUserLimitsCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new ArgumentException("e")); Assert.IsType<BadRequestObjectResult>(await _ctrl.UpdateUserLimits(Guid.NewGuid(), new UpdateUserLimitsRequest(10, 5))); }
    [Fact] public async Task UpdateLimits_500() { _med.Setup(m => m.Send(It.IsAny<UpdateUserLimitsCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("e")); var r = Assert.IsType<ObjectResult>(await _ctrl.UpdateUserLimits(Guid.NewGuid(), new UpdateUserLimitsRequest(10, 5))); Assert.Equal(500, r.StatusCode); }

    // ── CheckUserLimits ──
    [Fact]
    public async Task CheckLimits_Ok()
    {
        _med.Setup(m => m.Send(It.IsAny<CheckUserLimitsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckUserLimitsResponse(0, 1, 1, true, "Free", 0, DateTime.UtcNow.AddMonths(1), 1));
        Assert.IsType<OkObjectResult>((await _ctrl.CheckUserLimits(Guid.NewGuid())).Result);
    }

    [Fact] public async Task CheckLimits_KeyNotFound() { _med.Setup(m => m.Send(It.IsAny<CheckUserLimitsQuery>(), It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException("nf")); Assert.IsType<NotFoundObjectResult>((await _ctrl.CheckUserLimits(Guid.NewGuid())).Result); }
    [Fact] public async Task CheckLimits_500() { _med.Setup(m => m.Send(It.IsAny<CheckUserLimitsQuery>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("e")); var r = Assert.IsType<ObjectResult>((await _ctrl.CheckUserLimits(Guid.NewGuid())).Result); Assert.Equal(500, r.StatusCode); }

    // ── IncrementDocumentCount ──
    [Fact]
    public async Task Increment_Ok()
    {
        _med.Setup(m => m.Send(It.IsAny<IncrementDocumentCountCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IncrementDocumentCountResponse(1, 5, true));
        Assert.IsType<OkObjectResult>((await _ctrl.IncrementDocumentCount(Guid.NewGuid())).Result);
    }

    [Fact] public async Task Increment_KeyNotFound() { _med.Setup(m => m.Send(It.IsAny<IncrementDocumentCountCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new KeyNotFoundException("nf")); Assert.IsType<NotFoundObjectResult>((await _ctrl.IncrementDocumentCount(Guid.NewGuid())).Result); }
    [Fact] public async Task Increment_500() { _med.Setup(m => m.Send(It.IsAny<IncrementDocumentCountCommand>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("e")); var r = Assert.IsType<ObjectResult>((await _ctrl.IncrementDocumentCount(Guid.NewGuid())).Result); Assert.Equal(500, r.StatusCode); }

    // ── SyncCheckoutSession guard clauses ──
    [Fact]
    public async Task Sync_EmptySessionId_BadRequest()
    {
        SetClaims(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        var r = await _ctrl.SyncCheckoutSession(new SyncCheckoutSessionRequest(""));
        Assert.IsType<BadRequestObjectResult>(r.Result);
    }

    [Fact]
    public async Task Sync_NoAuth_Unauthorized()
    {
        SetNoAuth();
        var r = await _ctrl.SyncCheckoutSession(new SyncCheckoutSessionRequest("sess_123"));
        Assert.IsType<UnauthorizedResult>(r.Result);
    }

    [Fact]
    public async Task Sync_InvalidGuid_Unauthorized()
    {
        SetClaims(new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));
        var r = await _ctrl.SyncCheckoutSession(new SyncCheckoutSessionRequest("sess_123"));
        Assert.IsType<UnauthorizedResult>(r.Result);
    }

    // ── Checkout via sub claim ──
    [Fact]
    public async Task Checkout_SubClaim_Works()
    {
        var uid = Guid.NewGuid();
        SetClaims(new Claim("sub", uid.ToString()));
        _med.Setup(m => m.Send(It.IsAny<CreateCheckoutSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateCheckoutSessionResponse("https://url"));
        var r = await _ctrl.CreateCheckoutSession(new CreateCheckoutRequest("Bronze"));
        Assert.IsType<OkObjectResult>(r.Result);
    }

    // ── Portal via sub claim ──
    [Fact]
    public async Task Portal_SubClaim_Works()
    {
        SetClaims(new Claim("sub", Guid.NewGuid().ToString()));
        _med.Setup(m => m.Send(It.IsAny<CreatePortalSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreatePortalSessionResponse("https://portal"));
        Assert.IsType<OkObjectResult>((await _ctrl.CreatePortalSession()).Result);
    }

    // ── Portal invalid guid ──
    [Fact]
    public async Task Portal_InvalidGuid_Unauthorized()
    {
        SetClaims(new Claim(ClaimTypes.NameIdentifier, "not-a-guid"));
        Assert.IsType<UnauthorizedResult>((await _ctrl.CreatePortalSession()).Result);
    }
}
