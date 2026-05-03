using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentService.Domain.Entities;

namespace DocumentService.Application.Abstractions;

public interface IDocumentMessageRepository
{
    Task AddAsync(DocumentMessage message);
    Task AddRangeAsync(IEnumerable<DocumentMessage> messages);
    Task<List<DocumentMessage>> GetByDocumentIdAsync(Guid documentId);
}
