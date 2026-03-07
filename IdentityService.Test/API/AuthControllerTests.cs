using System.Security.Claims;
using IdentityService.API.Controllers;
using IdentityService.Application.Commands.LoginUser;
using IdentityService.Application.Commands.RegisterUser;
using IdentityService.Application.Queries.GetUserById;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace IdentityService.Test.API;

public class AuthControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new AuthController(_mediatorMock.Object);
    }

    [Fact]
    public async Task Given_ValidRegisterRequest_When_RegisterIsCalled_Then_ShouldReturnOkWithResponse()
    {
        var request = new RegisterRequest("test@example.com", "Pass123!", "Ion Popescu");
        var response = new RegisterUserResponse(Guid.NewGuid(), request.Email, request.FullName, DateTime.UtcNow);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RegisterUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.Register(request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<RegisterUserResponse>(okResult.Value);
        Assert.Equal(response, payload);
    }

    [Fact]
    public async Task Given_ExistingEmail_When_RegisterIsCalled_Then_ShouldReturnConflict()
    {
        var request = new RegisterRequest("existing@example.com", "Pass123!", "Ion Popescu");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RegisterUserCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("User with email already exists."));

        var result = await _controller.Register(request);

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Given_UnexpectedError_When_RegisterIsCalled_Then_ShouldReturnInternalServerError()
    {
        var request = new RegisterRequest("test@example.com", "Pass123!", "Ion Popescu");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<RegisterUserCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("db down"));

        var result = await _controller.Register(request);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }

    [Fact]
    public async Task Given_ValidLoginRequest_When_LoginIsCalled_Then_ShouldReturnOkWithResponse()
    {
        var request = new LoginRequest("test@example.com", "Pass123!");
        var response = new LoginUserResponse(Guid.NewGuid(), request.Email, "Ion Popescu", "token", DateTime.UtcNow.AddHours(1));

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<LoginUserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.Login(request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<LoginUserResponse>(okResult.Value);
        Assert.Equal(response, payload);
    }

    [Fact]
    public async Task Given_InvalidCredentials_When_LoginIsCalled_Then_ShouldReturnUnauthorized()
    {
        var request = new LoginRequest("test@example.com", "wrong");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<LoginUserCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        var result = await _controller.Login(request);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task Given_UnexpectedError_When_LoginIsCalled_Then_ShouldReturnInternalServerError()
    {
        var request = new LoginRequest("test@example.com", "Pass123!");

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<LoginUserCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));

        var result = await _controller.Login(request);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, objectResult.StatusCode);
    }

    [Fact]
    public async Task Given_NoUserIdClaim_When_GetCurrentUserIsCalled_Then_ShouldReturnUnauthorized()
    {
        SetClaimsPrincipal(Array.Empty<Claim>());

        var result = await _controller.GetCurrentUser();

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Given_InvalidUserIdClaim_When_GetCurrentUserIsCalled_Then_ShouldReturnUnauthorized()
    {
        SetClaimsPrincipal(new[] { new Claim(ClaimTypes.NameIdentifier, "not-a-guid") });

        var result = await _controller.GetCurrentUser();

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task Given_ValidSubClaimAndMissingUser_When_GetCurrentUserIsCalled_Then_ShouldReturnNotFound()
    {
        var userId = Guid.NewGuid();
        SetClaimsPrincipal(new[] { new Claim("sub", userId.ToString()) });

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetUserByIdQuery>(q => q.Id == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetUserByIdResponse?)null);

        var result = await _controller.GetCurrentUser();

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Given_ValidNameIdentifierClaim_When_GetCurrentUserIsCalled_Then_ShouldReturnOk()
    {
        var userId = Guid.NewGuid();
        var response = new GetUserByIdResponse(userId, "test@example.com", "Ion Popescu", DateTime.UtcNow, null);
        SetClaimsPrincipal(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) });

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetUserByIdQuery>(q => q.Id == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.GetCurrentUser();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<GetUserByIdResponse>(okResult.Value);
        Assert.Equal(response, payload);
    }

    [Fact]
    public async Task Given_ExistingUserId_When_GetUserIsCalled_Then_ShouldReturnOk()
    {
        var userId = Guid.NewGuid();
        var response = new GetUserByIdResponse(userId, "test@example.com", "Ion Popescu", DateTime.UtcNow, null);

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetUserByIdQuery>(q => q.Id == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _controller.GetUser(userId);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<GetUserByIdResponse>(okResult.Value);
    }

    [Fact]
    public async Task Given_MissingUserId_When_GetUserIsCalled_Then_ShouldReturnNotFound()
    {
        var userId = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.Is<GetUserByIdQuery>(q => q.Id == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetUserByIdResponse?)null);

        var result = await _controller.GetUser(userId);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    private void SetClaimsPrincipal(IEnumerable<Claim> claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }
}

