using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Abstractions;
using MediatR;

namespace IdentityService.Application.Queries.GetAllUsers;

public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, GetAllUsersResponse>
{
    private readonly IUserRepository _userRepository;

    public GetAllUsersQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<GetAllUsersResponse> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        var users = await _userRepository.GetAllAsync();

        var summaries = users.Select(u => new UserSummary(
            u.Id,
            u.Email,
            u.FullName,
            u.Role,
            u.CreatedAt,
            u.LastLoginAt,
            u.TotalDocumentsUploaded,
            u.MaxDocuments,
            u.MaxDocumentSizeMb,
            u.SubscriptionPlan.ToString(),
            u.MonthlyDocumentsUploaded,
            u.CurrentPeriodEnd
        ));

        return new GetAllUsersResponse(summaries);
    }
}
