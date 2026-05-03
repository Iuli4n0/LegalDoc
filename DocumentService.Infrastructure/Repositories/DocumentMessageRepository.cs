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
        await _context.DocumentMessages.AddAsync(message);
        await _context.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<DocumentMessage> messages)
    {
        await _context.DocumentMessages.AddRangeAsync(messages);
        await _context.SaveChangesAsync();
    }

    public async Task<List<DocumentMessage>> GetByDocumentIdAsync(Guid documentId)
    {
        return await _context.DocumentMessages
            .Where(m => m.DocumentId == documentId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }
}
