using System;
using System.Threading.Tasks;
using DocumentService.Domain.Entities;

namespace DocumentService.Application.Abstractions;

public interface IDocumentRepository
{
    Task AddAsync(Document document);
    Task<Document?> GetByIdAsync(Guid id);
    Task UpdateAsync(Document document);
}

