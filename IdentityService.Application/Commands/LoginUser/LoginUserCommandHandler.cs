using System;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Abstractions;
using MediatR;

namespace IdentityService.Application.Commands.LoginUser;

public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, LoginUserResponse>
{
    private const int TokenExpirationHours = 24;
    
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public LoginUserCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<LoginUserResponse> Handle(LoginUserCommand request, CancellationToken cancellationToken)
    {
        // Find user by email
        var user = await _userRepository.GetByEmailAsync(request.Email);
        
        if (user is null)
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        // Verify password
        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        // Update last login
        user.UpdateLastLogin();
        await _userRepository.UpdateAsync(user);

        // Generate JWT token
        var token = _jwtTokenGenerator.GenerateToken(user);

        return new LoginUserResponse(
            user.Id,
            user.Email,
            user.FullName,
            token,
            DateTime.UtcNow.AddHours(TokenExpirationHours)
        );
    }
}

