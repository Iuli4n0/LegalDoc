using System;
using System.Threading.Tasks;
using IdentityService.Domain.Entities;

namespace IdentityService.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
    Task<bool> ExistsAsync(string email);
}

