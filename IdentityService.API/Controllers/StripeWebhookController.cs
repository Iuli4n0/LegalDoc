using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;

namespace IdentityService.API.Controllers;

[ApiController]
[Route("api/stripe")]
public class StripeWebhookController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        IUserRepository userRepository,
        IConfiguration configuration,
        ILogger<StripeWebhookController> logger)
    {
        _userRepository = userRepository;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook([ModelBinder(typeof(RawStringModelBinder))] string json)
    {
        var webhookSecret = _configuration["Stripe:WebhookSecret"];

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                webhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed: {Message}", ex.Message);
            return BadRequest(new { error = "Webhook signature verification failed." });
        }

        _logger.LogInformation("Stripe webhook received: {EventType} ({EventId})", stripeEvent.Type, stripeEvent.Id);

        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
                await HandleCheckoutSessionCompleted(stripeEvent).ConfigureAwait(false);
                break;

            case EventTypes.CustomerSubscriptionUpdated:
                await HandleSubscriptionUpdated(stripeEvent).ConfigureAwait(false);
                break;

            case EventTypes.CustomerSubscriptionDeleted:
                await HandleSubscriptionDeleted(stripeEvent).ConfigureAwait(false);
                break;

            case EventTypes.InvoicePaymentFailed:
                _logger.LogWarning("Invoice payment failed: {EventId}", stripeEvent.Id);
                break;

            default:
                _logger.LogDebug("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
                break;
        }

        return Ok();
    }

    private async Task HandleCheckoutSessionCompleted(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
        if (session is null) return;

        var userId = session.Metadata?.GetValueOrDefault("userId");
        var planStr = session.Metadata?.GetValueOrDefault("plan");

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(planStr))
        {
            _logger.LogWarning("Checkout session completed but missing userId or plan metadata.");
            return;
        }

        if (!Guid.TryParse(userId, out var userGuid))
        {
            _logger.LogWarning("Invalid userId in checkout session metadata: {UserId}", userId);
            return;
        }

        if (!Enum.TryParse<SubscriptionPlan>(planStr, true, out var plan))
        {
            _logger.LogWarning("Invalid plan in checkout session metadata: {Plan}", planStr);
            return;
        }

        var user = await _userRepository.GetByIdAsync(userGuid).ConfigureAwait(false);
        if (user is null)
        {
            _logger.LogWarning("User {UserId} not found during checkout.session.completed", userId);
            return;
        }

        if (plan <= user.SubscriptionPlan)
        {
            _logger.LogWarning("Ignoring non-upgrade checkout completion for user {UserId}. Current: {CurrentPlan}, Requested: {RequestedPlan}",
                userId, user.SubscriptionPlan, plan);
            return;
        }

        var previousSubscriptionId = user.StripeSubscriptionId;

        // Set Stripe IDs
        if (!string.IsNullOrEmpty(session.CustomerId) && string.IsNullOrEmpty(user.StripeCustomerId))
        {
            user.SetStripeCustomerId(session.CustomerId);
        }

        user.UpdateSubscription(plan, session.SubscriptionId);
        user.ResetMonthlyCounter();

        await _userRepository.UpdateAsync(user).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(previousSubscriptionId)
            && !string.IsNullOrWhiteSpace(session.SubscriptionId)
            && !string.Equals(previousSubscriptionId, session.SubscriptionId, StringComparison.Ordinal))
        {
            try
            {
                var subscriptionService = new SubscriptionService();
                await subscriptionService.CancelAsync(previousSubscriptionId, new SubscriptionCancelOptions
                {
                    Prorate = false,
                    InvoiceNow = false
                }).ConfigureAwait(false);
                _logger.LogInformation("Cancelled previous Stripe subscription {PreviousSubscriptionId} for user {UserId}", previousSubscriptionId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cancel previous Stripe subscription {PreviousSubscriptionId} for user {UserId}", previousSubscriptionId, userId);
            }
        }

        _logger.LogInformation("User {UserId} upgraded to {Plan} plan (subscription: {SubscriptionId})",
            userId, plan, session.SubscriptionId);
    }

    private async Task HandleSubscriptionUpdated(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription is null) return;

        var userId = subscription.Metadata?.GetValueOrDefault("userId");
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            _logger.LogDebug("Subscription updated but no userId metadata. Subscription: {SubId}", subscription.Id);
            return;
        }

        var user = await _userRepository.GetByIdAsync(userGuid).ConfigureAwait(false);
        if (user is null) return;

        if (!string.IsNullOrWhiteSpace(user.StripeSubscriptionId)
            && !string.Equals(subscription.Id, user.StripeSubscriptionId, StringComparison.Ordinal))
        {
            _logger.LogDebug("Ignoring subscription.updated for non-current subscription {SubscriptionId}. Current is {CurrentSubscriptionId}",
                subscription.Id, user.StripeSubscriptionId);
            return;
        }

        // Check if cancelled via status
        if (subscription.Status == "canceled" || subscription.CancelAtPeriodEnd)
        {
            // Let it ride until period end, set the period end date
            user.SetCurrentPeriodEnd(subscription.CurrentPeriodEnd);
        }

        // Check for plan change via the price
        var planStr = subscription.Metadata?.GetValueOrDefault("plan");
        if (!string.IsNullOrEmpty(planStr) && Enum.TryParse<SubscriptionPlan>(planStr, true, out var plan))
        {
            if (plan > user.SubscriptionPlan)
            {
                user.UpdateSubscription(plan, subscription.Id);
                _logger.LogInformation("User {UserId} subscription changed to {Plan}", userId, plan);
            }
            else if (plan < user.SubscriptionPlan)
            {
                _logger.LogWarning("Ignoring downgrade subscription update for user {UserId}. Current: {CurrentPlan}, Requested: {RequestedPlan}",
                    userId, user.SubscriptionPlan, plan);
            }
        }

        // Sync period end
        user.SetCurrentPeriodEnd(subscription.CurrentPeriodEnd);

        await _userRepository.UpdateAsync(user).ConfigureAwait(false);
    }

    private async Task HandleSubscriptionDeleted(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription is null) return;

        var userId = subscription.Metadata?.GetValueOrDefault("userId");
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            // Try to find by customer ID
            if (!string.IsNullOrEmpty(subscription.CustomerId))
            {
                _logger.LogInformation("Subscription deleted for customer {CustomerId}, searching by customer ID", subscription.CustomerId);
            }
            return;
        }

        var user = await _userRepository.GetByIdAsync(userGuid).ConfigureAwait(false);
        if (user is null) return;

        if (!string.IsNullOrWhiteSpace(user.StripeSubscriptionId)
            && !string.Equals(subscription.Id, user.StripeSubscriptionId, StringComparison.Ordinal))
        {
            _logger.LogInformation("Ignoring delete for old subscription {DeletedSubscriptionId}; current active subscription is {CurrentSubscriptionId}",
                subscription.Id, user.StripeSubscriptionId);
            return;
        }

        user.UpdateSubscription(SubscriptionPlan.Free);
        user.ResetMonthlyCounter();

        await _userRepository.UpdateAsync(user).ConfigureAwait(false);

        _logger.LogInformation("User {UserId} subscription deleted, reverted to Free plan", userId);
    }
}

public class RawStringModelBinder : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        using var reader = new StreamReader(bindingContext.HttpContext.Request.Body);
        var value = await reader.ReadToEndAsync().ConfigureAwait(false);
        bindingContext.Result = ModelBindingResult.Success(value);
    }
}
