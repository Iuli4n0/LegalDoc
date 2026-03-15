using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentService.Domain.Entities;

namespace DocumentService.Application.Abstractions;

public interface IClauseRepository
{
    Task AddAsync(Clause clause);
    Task AddRangeAsync(IEnumerable<Clause> clauses);
    Task<IReadOnlyList<Clause>> GetByDocumentIdAsync(System.Guid documentId);
}
