using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

    public async Task<IReadOnlyList<Clause>> GetByDocumentIdAsync(Guid documentId)
    {
        return await _dbContext.Clauses
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.ExtractedAt)
            .ToListAsync();
    }

    public async Task ReplaceForDocumentAsync(Guid documentId, IReadOnlyList<Clause> clauses, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var existingClauses = await _dbContext.Clauses
            .Where(c => c.DocumentId == documentId)
            .ToListAsync(cancellationToken);

        if (existingClauses.Count > 0)
        {
            _dbContext.Clauses.RemoveRange(existingClauses);
        }

        if (clauses.Count > 0)
        {
            await _dbContext.Clauses.AddRangeAsync(clauses, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateRangeAsync(IReadOnlyList<Clause> clauses, CancellationToken cancellationToken = default)
    {
        _dbContext.Clauses.UpdateRange(clauses);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
