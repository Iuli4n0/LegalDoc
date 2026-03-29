using System;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Abstractions;
using MediatR;

namespace IdentityService.Application.Commands.UpdateUserLimits;

public class UpdateUserLimitsCommandHandler : IRequestHandler<UpdateUserLimitsCommand>
{
    private readonly IUserRepository _userRepository;

    public UpdateUserLimitsCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task Handle(UpdateUserLimitsCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);

        if (user is null)
            throw new KeyNotFoundException($"User {request.UserId} not found.");

        user.UpdateLimits(request.MaxDocuments, request.MaxDocumentSizeMb);
        await _userRepository.UpdateAsync(user);
    }
}
