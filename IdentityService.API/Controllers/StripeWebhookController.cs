using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Stripe;

namespace IdentityService.API.Controllers;

[ApiController]
[Route("api/stripe")]
public class StripeWebhookController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(
        IUserRepository userRepository,
        ILogger<StripeWebhookController> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook([ModelBinder(BinderType = typeof(StripeEventModelBinder))] Event? stripeEvent)
    {
        if (!ModelState.IsValid)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Invalid Stripe webhook payload or signature verification failed.");
            }

            return BadRequest(new { error = "Invalid webhook payload or signature verification failed." });
        }

        if (stripeEvent is null)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning("Stripe webhook model binding completed without an event.");
            }

            return BadRequest(new { error = "Invalid webhook payload." });
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Stripe webhook received: {EventType} ({EventId})", stripeEvent.Type, stripeEvent.Id);
        }

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
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning("Invoice payment failed: {EventId}", stripeEvent.Id);
                }

                break;

            default:
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
                }

                break;
        }

        return Ok();
    }

    private async Task HandleCheckoutSessionCompleted(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
        if (session is null) return;

        if (!TryGetCheckoutSessionContext(session, out var userId, out var userGuid, out var plan))
        {
            return;
        }

        var user = await _userRepository.GetByIdAsync(userGuid).ConfigureAwait(false);
        if (user is null)
        {
            LogCheckoutUserNotFound(userId);
            return;
        }

        if (plan <= user.SubscriptionPlan)
        {
            LogIgnoringNonUpgradeCheckoutCompletion(userId, user.SubscriptionPlan, plan);
            return;
        }

        await ApplyCheckoutSessionUpgradeAsync(session, user, plan, userId).ConfigureAwait(false);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User {UserId} upgraded to {Plan} plan (subscription: {SubscriptionId})",
                userId, plan, session.SubscriptionId);
        }
    }

    private bool TryGetCheckoutSessionContext(Stripe.Checkout.Session session, out string userId, out Guid userGuid, out SubscriptionPlan plan)
    {
        userId = string.Empty;
        userGuid = Guid.Empty;
        plan = default;

        var rawUserId = session.Metadata?.GetValueOrDefault("userId");
        var rawPlan = session.Metadata?.GetValueOrDefault("plan");

        if (string.IsNullOrEmpty(rawUserId) || string.IsNullOrEmpty(rawPlan))
        {
            LogCheckoutSessionMetadataMissing();
            return false;
        }

        if (!Guid.TryParse(rawUserId, out userGuid))
        {
            LogInvalidCheckoutUserId(rawUserId);
            return false;
        }

        if (!Enum.TryParse<SubscriptionPlan>(rawPlan, true, out plan))
        {
            LogInvalidCheckoutPlan(rawPlan);
            return false;
        }

        userId = rawUserId;
        return true;
    }

    private void LogCheckoutSessionMetadataMissing()
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("Checkout session completed but missing userId or plan metadata.");
        }
    }

    private void LogInvalidCheckoutUserId(string userId)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("Invalid userId in checkout session metadata: {UserId}", userId);
        }
    }

    private void LogInvalidCheckoutPlan(string planStr)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("Invalid plan in checkout session metadata: {Plan}", planStr);
        }
    }

    private void LogCheckoutUserNotFound(string userId)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("User {UserId} not found during checkout.session.completed", userId);
        }
    }

    private void LogIgnoringNonUpgradeCheckoutCompletion(string userId, SubscriptionPlan currentPlan, SubscriptionPlan requestedPlan)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("Ignoring non-upgrade checkout completion for user {UserId}. Current: {CurrentPlan}, Requested: {RequestedPlan}",
                userId, currentPlan, requestedPlan);
        }
    }

    private async Task ApplyCheckoutSessionUpgradeAsync(Stripe.Checkout.Session session, User user, SubscriptionPlan plan, string userId)
    {
        var previousSubscriptionId = user.StripeSubscriptionId;

        // Set Stripe IDs
        if (!string.IsNullOrEmpty(session.CustomerId) && string.IsNullOrEmpty(user.StripeCustomerId))
        {
            user.SetStripeCustomerId(session.CustomerId);
        }

        user.UpdateSubscription(plan, session.SubscriptionId);
        user.ResetMonthlyCounter();

        await _userRepository.UpdateAsync(user).ConfigureAwait(false);
        await CancelPreviousStripeSubscriptionIfNeededAsync(previousSubscriptionId, session.SubscriptionId, userId).ConfigureAwait(false);
    }

    private async Task CancelPreviousStripeSubscriptionIfNeededAsync(string? previousSubscriptionId, string? currentSubscriptionId, string userId)
    {
        if (string.IsNullOrWhiteSpace(previousSubscriptionId)
            || string.IsNullOrWhiteSpace(currentSubscriptionId)
            || string.Equals(previousSubscriptionId, currentSubscriptionId, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var subscriptionService = new SubscriptionService();
            await subscriptionService.CancelAsync(previousSubscriptionId, new SubscriptionCancelOptions
            {
                Prorate = false,
                InvoiceNow = false
            }).ConfigureAwait(false);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Cancelled previous Stripe subscription {PreviousSubscriptionId} for user {UserId}", previousSubscriptionId, userId);
            }
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(ex, "Failed to cancel previous Stripe subscription {PreviousSubscriptionId} for user {UserId}", previousSubscriptionId, userId);
            }
        }
    }

    private async Task HandleSubscriptionUpdated(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription is null) return;

        if (!TryGetSubscriptionUserId(subscription, out var userId, out var userGuid))
        {
            return;
        }

        var user = await _userRepository.GetByIdAsync(userGuid).ConfigureAwait(false);
        if (user is null) return;

        if (ShouldIgnoreNonCurrentSubscription(subscription, user))
        {
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
            ApplySubscriptionPlanChange(subscription, user, userId, plan);
        }

        // Sync period end
        user.SetCurrentPeriodEnd(subscription.CurrentPeriodEnd);

        await _userRepository.UpdateAsync(user).ConfigureAwait(false);
    }

    private bool TryGetSubscriptionUserId(Subscription subscription, out string userId, out Guid userGuid)
    {
        var rawUserId = subscription.Metadata?.GetValueOrDefault("userId");
        userId = rawUserId ?? string.Empty;
        userGuid = Guid.Empty;

        if (string.IsNullOrEmpty(rawUserId) || !Guid.TryParse(rawUserId, out userGuid))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Subscription updated but no userId metadata. Subscription: {SubId}", subscription.Id);
            }

            return false;
        }

        return true;
    }

    private bool ShouldIgnoreNonCurrentSubscription(Subscription subscription, User user)
    {
        if (string.IsNullOrWhiteSpace(user.StripeSubscriptionId)
            || string.Equals(subscription.Id, user.StripeSubscriptionId, StringComparison.Ordinal))
        {
            return false;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Ignoring subscription.updated for non-current subscription {SubscriptionId}. Current is {CurrentSubscriptionId}",
                subscription.Id, user.StripeSubscriptionId);
        }

        return true;
    }

    private void ApplySubscriptionPlanChange(Subscription subscription, User user, string userId, SubscriptionPlan plan)
    {
        if (plan > user.SubscriptionPlan)
        {
            user.UpdateSubscription(plan, subscription.Id);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("User {UserId} subscription changed to {Plan}", userId, plan);
            }
        }
        else if (plan < user.SubscriptionPlan && _logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning("Ignoring downgrade subscription update for user {UserId}. Current: {CurrentPlan}, Requested: {RequestedPlan}",
                userId, user.SubscriptionPlan, plan);
        }
    }

    private async Task HandleSubscriptionDeleted(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription is null) return;

        var userId = subscription.Metadata?.GetValueOrDefault("userId");
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            // Try to find by customer ID
            if (!string.IsNullOrEmpty(subscription.CustomerId) && _logger.IsEnabled(LogLevel.Information))
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
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Ignoring delete for old subscription {DeletedSubscriptionId}; current active subscription is {CurrentSubscriptionId}",
                    subscription.Id, user.StripeSubscriptionId);
            }

            return;
        }

        user.UpdateSubscription(SubscriptionPlan.Free);
        user.ResetMonthlyCounter();

        await _userRepository.UpdateAsync(user).ConfigureAwait(false);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("User {UserId} subscription deleted, reverted to Free plan", userId);
        }
    }
}

public class StripeEventModelBinder : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var httpContext = bindingContext.HttpContext;
        var request = httpContext.Request;
        var services = httpContext.RequestServices;
        var configuration = services.GetService(typeof(IConfiguration)) as IConfiguration;
        var loggerFactory = services.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
        var logger = loggerFactory?.CreateLogger("StripeEventModelBinder");

        if (configuration is null)
        {
            bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Configuration unavailable");
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        var webhookSecret = configuration["Stripe:WebhookSecret"];

        if (string.IsNullOrEmpty(webhookSecret))
        {
            bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Webhook secret not configured");
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        // Ensure the body can be read multiple times
        request.EnableBuffering();
        string json;
        try
        {
            request.Body.Position = 0;
            using var reader = new StreamReader(request.Body, leaveOpen: true);
            json = await reader.ReadToEndAsync().ConfigureAwait(false);
            request.Body.Position = 0; // rewind for other components
        }
        catch (Exception ex)
        {
            if (logger?.IsEnabled(LogLevel.Warning) == true)
            {
                logger.LogWarning(ex, "Failed to read request body for Stripe webhook");
            }

            bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Failed to read request body");
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        var sigHeader = request.Headers["Stripe-Signature"].FirstOrDefault();
        if (string.IsNullOrEmpty(sigHeader))
        {
            bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Missing Stripe-Signature header");
            bindingContext.Result = ModelBindingResult.Failed();
            return;
        }

        try
        {
            var stripeEvent = EventUtility.ConstructEvent(json, sigHeader, webhookSecret);
            bindingContext.Result = ModelBindingResult.Success(stripeEvent);
        }
        catch (StripeException ex)
        {
            if (logger?.IsEnabled(LogLevel.Warning) == true)
            {
                logger.LogWarning(ex, "Stripe webhook signature verification failed: {Message}", ex.Message);
            }

            bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Signature verification failed");
            bindingContext.Result = ModelBindingResult.Failed();
        }
        catch (Exception ex)
        {
            if (logger?.IsEnabled(LogLevel.Warning) == true)
            {
                logger.LogWarning(ex, "Failed to construct Stripe event from payload");
            }

            bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Invalid Stripe event payload");
            bindingContext.Result = ModelBindingResult.Failed();
        }
    }
}
