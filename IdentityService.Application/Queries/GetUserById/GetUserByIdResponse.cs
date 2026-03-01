using System;

namespace IdentityService.Application.Queries.GetUserById;

public record GetUserByIdResponse(
    Guid Id,
    string Email,
    string FullName,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

