using IdentityService.Domain.Entities;
using MediatR;

namespace IdentityService.Application.Commands.CreateCheckoutSession;

public record CreateCheckoutSessionCommand(System.Guid UserId, SubscriptionPlan Plan) : IRequest<CreateCheckoutSessionResponse>;

public record CreateCheckoutSessionResponse(string CheckoutUrl);
