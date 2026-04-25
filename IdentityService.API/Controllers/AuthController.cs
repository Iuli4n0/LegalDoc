using System;
using IdentityService.Application.Abstractions;
using IdentityService.Application.Commands.CreateCheckoutSession;
using IdentityService.Application.Commands.CreatePortalSession;
using IdentityService.Application.Commands.IncrementDocumentCount;
using IdentityService.Application.Commands.LoginUser;
using IdentityService.Application.Commands.RegisterUser;
using IdentityService.Application.Commands.UpdateUserLimits;
using IdentityService.Application.Queries.CheckUserLimits;
using IdentityService.Application.Queries.GetAllUsers;
using IdentityService.Application.Queries.GetUserById;
using IdentityService.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe.Checkout;
using Stripe;

namespace IdentityService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const int InternalServerErrorStatusCode = 500;
    
    private readonly IMediator _mediator;
    private readonly IUserRepository _userRepository;

    public AuthController(IMediator mediator, IUserRepository userRepository)
    {
        _mediator = mediator;
        _userRepository = userRepository;
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

    // ── Stripe subscription endpoints ──────────────────────

    [Authorize]
    [HttpPost("create-checkout-session")]
    public async Task<ActionResult<CreateCheckoutSessionResponse>> CreateCheckoutSession([FromBody] CreateCheckoutRequest request)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            if (!Enum.TryParse<SubscriptionPlan>(request.Plan, true, out var plan) || plan == SubscriptionPlan.Free)
                return BadRequest(new { message = "Invalid plan. Choose Bronze, Silver, or Gold." });

            var command = new CreateCheckoutSessionCommand(userId, plan);
            var response = await _mediator.Send(command);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, new { message = $"Failed to create checkout session: {ex.Message}" });
        }
    }

    [Authorize]
    [HttpPost("create-portal-session")]
    public async Task<ActionResult<CreatePortalSessionResponse>> CreatePortalSession()
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var command = new CreatePortalSessionCommand(userId);
            var response = await _mediator.Send(command);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, new { message = $"Failed to create portal session: {ex.Message}" });
        }
    }

    // ── Admin endpoints ──────────────────────────────────────

    [Authorize(Roles = "Admin")]
    [HttpGet("users")]
    public async Task<ActionResult<GetAllUsersResponse>> GetAllUsers()
    {
        try
        {
            var query = new GetAllUsersQuery();
            var response = await _mediator.Send(query);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, new { message = $"Failed to retrieve users: {ex.Message}" });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("users/{id:guid}/limits")]
    public async Task<IActionResult> UpdateUserLimits(Guid id, [FromBody] UpdateUserLimitsRequest request)
    {
        try
        {
            var command = new UpdateUserLimitsCommand(id, request.MaxDocuments, request.MaxDocumentSizeMb);
            await _mediator.Send(command);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, new { message = $"Failed to update limits: {ex.Message}" });
        }
    }

    [Authorize]
    [HttpGet("users/{id:guid}/limits")]
    public async Task<ActionResult<CheckUserLimitsResponse>> CheckUserLimits(Guid id)
    {
        try
        {
            var query = new CheckUserLimitsQuery(id);
            var response = await _mediator.Send(query);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, new { message = $"Failed to check limits: {ex.Message}" });
        }
    }

    [Authorize]
    [HttpPost("users/{id:guid}/increment-documents")]
    public async Task<ActionResult<IncrementDocumentCountResponse>> IncrementDocumentCount(Guid id)
    {
        try
        {
            var command = new IncrementDocumentCountCommand(id);
            var response = await _mediator.Send(command);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, new { message = $"Failed to increment count: {ex.Message}" });
        }
    }

    [Authorize]
    [HttpPost("sync-checkout-session")]
    public async Task<ActionResult<SyncCheckoutSessionResponse>> SyncCheckoutSession([FromBody] SyncCheckoutSessionRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.SessionId))
                return BadRequest(new { message = "SessionId is required." });

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var user = await _userRepository.GetByIdAsync(userId);
            if (user is null)
                return NotFound(new { message = $"User {userId} not found." });

            var sessionService = new SessionService();
            var session = await sessionService.GetAsync(request.SessionId);
            if (session is null)
                return NotFound(new { message = "Stripe checkout session not found." });

            var sessionUserId = session.Metadata?.GetValueOrDefault("userId");
            var sessionPlan = session.Metadata?.GetValueOrDefault("plan");

            var sameUserByMetadata = Guid.TryParse(sessionUserId, out var sessionUserGuid) && sessionUserGuid == userId;
            var sameUserByCustomerId = !string.IsNullOrEmpty(user.StripeCustomerId)
                                       && !string.IsNullOrEmpty(session.CustomerId)
                                       && string.Equals(user.StripeCustomerId, session.CustomerId, StringComparison.Ordinal);

            if (!sameUserByMetadata && !sameUserByCustomerId)
                return Forbid();

            if (!string.Equals(session.Status, "complete", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Checkout session is not complete yet." });

            if (!Enum.TryParse<SubscriptionPlan>(sessionPlan, true, out var plan) || plan == SubscriptionPlan.Free)
                return BadRequest(new { message = "Could not determine purchased plan from Stripe session metadata." });

            if (plan <= user.SubscriptionPlan)
                return BadRequest(new { message = "Only upgrades to a higher plan are allowed." });

            var previousSubscriptionId = user.StripeSubscriptionId;

            if (!string.IsNullOrEmpty(session.CustomerId) && string.IsNullOrEmpty(user.StripeCustomerId))
            {
                user.SetStripeCustomerId(session.CustomerId);
            }

            user.UpdateSubscription(plan, session.SubscriptionId);
            user.ResetMonthlyCounter();

            await _userRepository.UpdateAsync(user);

            if (!string.IsNullOrWhiteSpace(previousSubscriptionId)
                && !string.IsNullOrWhiteSpace(session.SubscriptionId)
                && !string.Equals(previousSubscriptionId, session.SubscriptionId, StringComparison.Ordinal))
            {
                var subscriptionService = new SubscriptionService();
                await subscriptionService.CancelAsync(previousSubscriptionId, new SubscriptionCancelOptions
                {
                    Prorate = false,
                    InvoiceNow = false
                });
            }

            return Ok(new SyncCheckoutSessionResponse(
                user.SubscriptionPlan.ToString(),
                user.MaxDocuments,
                user.MaxDocumentSizeMb,
                user.MonthlyDocumentsUploaded,
                user.CurrentPeriodEnd));
        }
        catch (Stripe.StripeException ex)
        {
            return BadRequest(new { message = $"Stripe error while syncing session: {ex.Message}" });
        }
        catch (Exception ex)
        {
            return StatusCode(InternalServerErrorStatusCode, new { message = $"Failed to sync checkout session: {ex.Message}" });
        }
    }
}

// Request DTOs
public record RegisterRequest(string Email, string Password, string FullName);
public record LoginRequest(string Email, string Password);
public record UpdateUserLimitsRequest(int MaxDocuments, int MaxDocumentSizeMb);
public record CreateCheckoutRequest(string Plan);
public record SyncCheckoutSessionRequest(string SessionId);
public record SyncCheckoutSessionResponse(
    string SubscriptionPlan,
    int MaxDocuments,
    int MaxDocumentSizeMb,
    int MonthlyDocumentsUploaded,
    DateTime CurrentPeriodEnd);
