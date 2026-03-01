using System;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.Entities;
using MediatR;

namespace IdentityService.Application.Commands.RegisterUser;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, RegisterUserResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public RegisterUserCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<RegisterUserResponse> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        // Check if user already exists
        if (await _userRepository.ExistsAsync(request.Email))
        {
            throw new InvalidOperationException($"User with email '{request.Email}' already exists.");
        }

        // Hash password
        var passwordHash = _passwordHasher.Hash(request.Password);

        // Create user
        var user = User.Create(request.Email, passwordHash, request.FullName);

        // Save to database
        await _userRepository.AddAsync(user);

        return new RegisterUserResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.CreatedAt
        );
    }
}

