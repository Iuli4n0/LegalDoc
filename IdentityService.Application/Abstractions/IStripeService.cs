using System;
using System.Threading.Tasks;
using IdentityService.Domain.Entities;

namespace IdentityService.Application.Abstractions;

public interface IStripeService
{
    Task<string> CreateCheckoutSessionAsync(Guid userId, string email, SubscriptionPlan plan);
    Task<string> CreateCustomerPortalSessionAsync(string stripeCustomerId);
}
