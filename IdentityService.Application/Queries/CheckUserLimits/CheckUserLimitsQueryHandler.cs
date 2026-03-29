using System;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Abstractions;
using MediatR;

namespace IdentityService.Application.Queries.CheckUserLimits;

public class CheckUserLimitsQueryHandler : IRequestHandler<CheckUserLimitsQuery, CheckUserLimitsResponse>
{
    private readonly IUserRepository _userRepository;

    public CheckUserLimitsQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<CheckUserLimitsResponse> Handle(CheckUserLimitsQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);

        if (user is null)
            throw new KeyNotFoundException($"User {request.UserId} not found.");

        return new CheckUserLimitsResponse(
            user.TotalDocumentsUploaded,
            user.MaxDocuments,
            user.MaxDocumentSizeMb,
            user.CanUploadDocument()
        );
    }
}
