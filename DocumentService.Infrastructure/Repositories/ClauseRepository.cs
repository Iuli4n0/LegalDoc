using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using DocumentService.Domain.Entities;
using DocumentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocumentService.Infrastructure.Repositories;

public class ClauseRepository : IClauseRepository
{
    private readonly AppDbContext _dbContext;

    public ClauseRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Clause clause)
    {
        await _dbContext.Clauses.AddAsync(clause);
        await _dbContext.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<Clause> clauses)
    {
        await _dbContext.Clauses.AddRangeAsync(clauses);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<Clause>> GetByDocumentIdAsync(System.Guid documentId)
    {
        return await _dbContext.Clauses
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.ExtractedAt)
            .ToListAsync();
    }
}
