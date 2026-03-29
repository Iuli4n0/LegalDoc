using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Domain.Entities;

namespace DocumentService.Application.Abstractions;

public interface IClauseRepository
{
    Task AddAsync(Clause clause);
    Task AddRangeAsync(IEnumerable<Clause> clauses);
    Task<IReadOnlyList<Clause>> GetByDocumentIdAsync(Guid documentId);
    Task ReplaceForDocumentAsync(Guid documentId, IReadOnlyList<Clause> clauses, CancellationToken cancellationToken = default);
    Task UpdateRangeAsync(IReadOnlyList<Clause> clauses, CancellationToken cancellationToken = default);
}
