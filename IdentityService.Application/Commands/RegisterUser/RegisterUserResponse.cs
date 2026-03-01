using System;

namespace IdentityService.Application.Commands.RegisterUser;

public record RegisterUserResponse(
    Guid Id,
    string Email,
    string FullName,
    DateTime CreatedAt
);

