using System;
using IdentityService.Domain.Entities;

namespace IdentityService.Application.Abstractions;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user);
}

