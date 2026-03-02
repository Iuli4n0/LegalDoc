using MediatR;

namespace IdentityService.Application.Commands.RegisterUser;

public class RegisterUserCommand : IRequest<RegisterUserResponse>
{
    public string Email { get; init; } = null!;
    public string Password { get; init; } = null!;
    public string FullName { get; init; } = null!;
}

