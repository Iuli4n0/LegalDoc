using System;
using System.Threading.Tasks;
using IdentityService.Application.Abstractions;
using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _dbContext;

    public UserRepository(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var normalizedEmail = email.ToLowerInvariant();
        return await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
    }

    public async Task AddAsync(User user)
    {
        await _dbContext.Users.AddAsync(user);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(User user)
    {
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> ExistsAsync(string email)
    {
        var normalizedEmail = email.ToLowerInvariant();
        return await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail);
    }
}

