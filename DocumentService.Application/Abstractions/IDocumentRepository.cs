using System;
using System.Threading.Tasks;
using DocumentService.Domain.Entities;

namespace DocumentService.Application.Interfaces;

public interface IDocumentRepository
{
    Task AddAsync(Document document);
    Task<Document?> GetByIdAsync(Guid id);
}

