using System;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Abstractions;
using MediatR;

namespace IdentityService.Application.Commands.IncrementDocumentCount;

public class IncrementDocumentCountCommandHandler : IRequestHandler<IncrementDocumentCountCommand, IncrementDocumentCountResponse>
{
    private readonly IUserRepository _userRepository;

    public IncrementDocumentCountCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<IncrementDocumentCountResponse> Handle(IncrementDocumentCountCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);

        if (user is null)
            throw new KeyNotFoundException($"User {request.UserId} not found.");

        user.IncrementDocumentCount();
        await _userRepository.UpdateAsync(user);

        return new IncrementDocumentCountResponse(
            user.TotalDocumentsUploaded,
            user.MaxDocuments,
            user.CanUploadDocument()
        );
    }
}
