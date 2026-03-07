using System;
using IdentityService.Application.Commands.LoginUser;
using IdentityService.Application.Commands.RegisterUser;
using IdentityService.Application.Queries.GetUserById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const int InternalServerErrorStatusCode = 500;
    
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterUserResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var command = new RegisterUserCommand
            {
                Email = request.Email,
                Password = request.Password,
                FullName = request.FullName
            };

            var response = await _mediator.Send(command);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, new { message = $"Registration failed: {ex.Message}" });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginUserResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var command = new LoginUserCommand
            {
                Email = request.Email,
                Password = request.Password
            };

            var response = await _mediator.Send(command);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, new { message = $"Login failed: {ex.Message}" });
        }
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<GetUserByIdResponse>> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                          ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var query = new GetUserByIdQuery(userId);
        var response = await _mediator.Send(query);

        if (response is null)
            return NotFound();

        return Ok(response);
    }

    [Authorize]
    [HttpGet("users/{id:guid}")]
    public async Task<ActionResult<GetUserByIdResponse>> GetUser(Guid id)
    {
        var query = new GetUserByIdQuery(id);
        var response = await _mediator.Send(query);

        if (response is null)
            return NotFound();

        return Ok(response);
    }
}

// Request DTOs
public record RegisterRequest(string Email, string Password, string FullName);
public record LoginRequest(string Email, string Password);

