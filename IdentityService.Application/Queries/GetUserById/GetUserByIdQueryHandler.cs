using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Abstractions;
using MediatR;

namespace IdentityService.Application.Queries.GetUserById;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, GetUserByIdResponse?>
{
    private readonly IUserRepository _userRepository;

    public GetUserByIdQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<GetUserByIdResponse?> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.Id);

        if (user is null)
            return null;

        return new GetUserByIdResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.CreatedAt,
            user.LastLoginAt
        );
    }
}

