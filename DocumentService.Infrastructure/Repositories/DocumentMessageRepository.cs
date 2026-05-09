using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using DocumentService.Domain.Entities;
using DocumentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocumentService.Infrastructure.Repositories;

public class DocumentMessageRepository : IDocumentMessageRepository
{
    private readonly AppDbContext _context;

    public DocumentMessageRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(DocumentMessage message)
    {
        await _context.DocumentMessages.AddAsync(message).ConfigureAwait(false);
        await _context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task AddRangeAsync(IEnumerable<DocumentMessage> messages)
    {
        await _context.DocumentMessages.AddRangeAsync(messages).ConfigureAwait(false);
        await _context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<List<DocumentMessage>> GetByDocumentIdAsync(Guid documentId)
    {
        return await _context.DocumentMessages
            .Where(m => m.DocumentId == documentId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync().ConfigureAwait(false);
    }
}
