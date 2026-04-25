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

        // CanUploadDocument triggers lazy monthly reset internally
        var canUpload = user.CanUploadDocument();

        // If the monthly counter was reset, persist the change
        await _userRepository.UpdateAsync(user);

        return new CheckUserLimitsResponse(
            user.TotalDocumentsUploaded,
            user.MaxDocuments,
            user.MaxDocumentSizeMb,
            canUpload,
            user.SubscriptionPlan.ToString(),
            user.MonthlyDocumentsUploaded,
            user.CurrentPeriodEnd,
            user.GetRemainingUploads()
        );
    }
}
