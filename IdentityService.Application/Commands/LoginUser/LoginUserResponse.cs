using System;

namespace IdentityService.Application.Commands.LoginUser;

public record LoginUserResponse(
    Guid UserId,
    string Email,
    string FullName,
    string Token,
    DateTime ExpiresAt
);

