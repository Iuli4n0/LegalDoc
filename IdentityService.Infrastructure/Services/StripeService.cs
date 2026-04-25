using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;

namespace IdentityService.Infrastructure.Services;

public class StripeService : IStripeService
{
    private readonly IConfiguration _configuration;
    private readonly IUserRepository _userRepository;

    public StripeService(IConfiguration configuration, IUserRepository userRepository)
    {
        _configuration = configuration;
        _userRepository = userRepository;
    }

    public async Task<string> CreateCheckoutSessionAsync(Guid userId, string email, SubscriptionPlan plan)
    {
        var priceId = GetPriceId(plan);
        var successUrl = _configuration["Stripe:SuccessUrl"] ?? "http://localhost:5288/subscription/success";
        var cancelUrl = _configuration["Stripe:CancelUrl"] ?? "http://localhost:5288/subscription/cancel";

        // Ensure user has a Stripe customer
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
            throw new KeyNotFoundException($"User {userId} not found.");

        if (plan <= user.SubscriptionPlan)
            throw new ArgumentException("Only upgrades to a higher plan are allowed.");

        string customerId;
        if (string.IsNullOrEmpty(user.StripeCustomerId))
        {
            var customerService = new CustomerService();
            var customer = await customerService.CreateAsync(new CustomerCreateOptions
            {
                Email = email,
                Metadata = new Dictionary<string, string>
                {
                    { "userId", userId.ToString() }
                }
            });
            customerId = customer.Id;
            user.SetStripeCustomerId(customerId);
            await _userRepository.UpdateAsync(user);
        }
        else
        {
            customerId = user.StripeCustomerId;
        }

        // Credit the amount of current plan once when user upgrades.
        var discountOptions = await BuildUpgradeDiscountAsync(user.SubscriptionPlan, plan);

        var sessionOptions = new SessionCreateOptions
        {
            Customer = customerId,
            Mode = "subscription",
            SuccessUrl = successUrl + "?session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = cancelUrl,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = priceId,
                    Quantity = 1
                }
            ],
            Metadata = new Dictionary<string, string>
            {
                { "userId", userId.ToString() },
                { "plan", plan.ToString() }
            },
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    { "userId", userId.ToString() },
                    { "plan", plan.ToString() }
                }
            }
        };

        if (discountOptions is not null)
        {
            sessionOptions.Discounts =
            [
                discountOptions
            ];
        }

        var sessionService = new SessionService();
        var session = await sessionService.CreateAsync(sessionOptions);

        return session.Url;
    }

    public async Task<string> CreateCustomerPortalSessionAsync(string stripeCustomerId)
    {
        var returnUrl = _configuration["Stripe:PortalReturnUrl"] 
                        ?? _configuration["Stripe:SuccessUrl"] 
                        ?? "http://localhost:5288/profile";

        var portalService = new Stripe.BillingPortal.SessionService();
        var session = await portalService.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = stripeCustomerId,
            ReturnUrl = returnUrl
        });

        return session.Url;
    }

    private string GetPriceId(SubscriptionPlan plan)
    {
        var section = _configuration.GetSection("Stripe:Prices");
        var priceId = section[plan.ToString()];

        if (string.IsNullOrEmpty(priceId))
            throw new InvalidOperationException($"Stripe Price ID not configured for plan '{plan}'. Add Stripe:Prices:{plan} to configuration.");

        return priceId;
    }

    private async Task<SessionDiscountOptions?> BuildUpgradeDiscountAsync(SubscriptionPlan currentPlan, SubscriptionPlan targetPlan)
    {
        var currentAmountRon = GetPlanAmountRon(currentPlan);
        var targetAmountRon = GetPlanAmountRon(targetPlan);

        if (currentAmountRon <= 0 || targetAmountRon <= 0)
            return null;

        var amountOffRon = Math.Min(currentAmountRon, targetAmountRon - 1);
        if (amountOffRon <= 0)
            return null;

        var couponService = new CouponService();
        var coupon = await couponService.CreateAsync(new CouponCreateOptions
        {
            Duration = "once",
            Currency = "ron",
            AmountOff = amountOffRon * 100,
            Name = $"Upgrade credit: {currentPlan}"
        });

        return new SessionDiscountOptions
        {
            Coupon = coupon.Id
        };
    }

    private int GetPlanAmountRon(SubscriptionPlan plan)
    {
        var configured = _configuration[$"Stripe:PlanAmounts:{plan}"];
        if (int.TryParse(configured, out var configuredAmount) && configuredAmount >= 0)
            return configuredAmount;

        return plan switch
        {
            SubscriptionPlan.Free => 0,
            SubscriptionPlan.Bronze => 50,
            SubscriptionPlan.Silver => 100,
            SubscriptionPlan.Gold => 200,
            _ => 0
        };
    }
}
