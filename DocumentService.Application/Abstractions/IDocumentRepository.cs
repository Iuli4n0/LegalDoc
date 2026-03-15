using System;
using System.Threading.Tasks;
using DocumentService.Domain.Entities;

namespace DocumentService.Application.Abstractions;

public interface IDocumentRepository
{
    Task AddAsync(Document document);
    Task<Document?> GetByIdAsync(Guid id);
    Task UpdateAsync(Document document);
    Task DeleteAsync(Document document);
    Task<IEnumerable<Document>> GetByUserIdAsync(string userId, int page, int pageSize, string sortBy, bool ascending);
    Task<int> CountByUserIdAsync(string userId);
}

