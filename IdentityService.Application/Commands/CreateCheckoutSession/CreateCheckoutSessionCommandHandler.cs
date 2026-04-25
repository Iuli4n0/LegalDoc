using System;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Abstractions;
using MediatR;

namespace IdentityService.Application.Commands.CreateCheckoutSession;

public class CreateCheckoutSessionCommandHandler : IRequestHandler<CreateCheckoutSessionCommand, CreateCheckoutSessionResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IStripeService _stripeService;

    public CreateCheckoutSessionCommandHandler(IUserRepository userRepository, IStripeService stripeService)
    {
        _userRepository = userRepository;
        _stripeService = stripeService;
    }

    public async Task<CreateCheckoutSessionResponse> Handle(CreateCheckoutSessionCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);

        if (user is null)
            throw new KeyNotFoundException($"User {request.UserId} not found.");

        if (request.Plan == Domain.Entities.SubscriptionPlan.Free)
            throw new ArgumentException("Cannot create a checkout session for the Free plan.");

        if (request.Plan <= user.SubscriptionPlan)
            throw new ArgumentException("You can only upgrade to a higher plan than your current one.");

        var checkoutUrl = await _stripeService.CreateCheckoutSessionAsync(
            request.UserId, user.Email, request.Plan);

        return new CreateCheckoutSessionResponse(checkoutUrl);
    }
}
