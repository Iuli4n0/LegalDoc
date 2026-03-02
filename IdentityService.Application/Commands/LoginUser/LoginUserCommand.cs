using MediatR;

namespace IdentityService.Application.Commands.LoginUser;

public class LoginUserCommand : IRequest<LoginUserResponse>
{
    public string Email { get; init; } = null!;
    public string Password { get; init; } = null!;
}

