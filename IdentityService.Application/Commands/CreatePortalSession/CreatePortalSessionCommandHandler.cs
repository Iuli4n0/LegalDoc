using System;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Abstractions;
using MediatR;

namespace IdentityService.Application.Commands.CreatePortalSession;

public class CreatePortalSessionCommandHandler : IRequestHandler<CreatePortalSessionCommand, CreatePortalSessionResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IStripeService _stripeService;

    public CreatePortalSessionCommandHandler(IUserRepository userRepository, IStripeService stripeService)
    {
        _userRepository = userRepository;
        _stripeService = stripeService;
    }

    public async Task<CreatePortalSessionResponse> Handle(CreatePortalSessionCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId).ConfigureAwait(false);

        if (user is null)
            throw new KeyNotFoundException($"User {request.UserId} not found.");

        if (string.IsNullOrEmpty(user.StripeCustomerId))
            throw new InvalidOperationException("User does not have a Stripe customer account. Please subscribe to a plan first.");

        var portalUrl = await _stripeService.CreateCustomerPortalSessionAsync(user.StripeCustomerId).ConfigureAwait(false);

        return new CreatePortalSessionResponse(portalUrl);
    }
}
