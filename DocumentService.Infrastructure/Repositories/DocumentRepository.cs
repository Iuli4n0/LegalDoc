using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using DocumentService.Domain.Entities;
using DocumentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocumentService.Infrastructure.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _dbContext;

    public DocumentRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Document document)
    {
        await _dbContext.Documents.AddAsync(document);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<Document?> GetByIdAsync(Guid id)
    {
        return await _dbContext.Documents.FirstOrDefaultAsync(d => d.Id == id);
    }

    public async Task UpdateAsync(Document document)
    {
        _dbContext.Documents.Update(document);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(Document document)
    {
        _dbContext.Documents.Remove(document);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<IEnumerable<Document>> GetByUserIdAsync(
        string userId, int page, int pageSize, string sortBy, bool ascending)
    {
        var query = _dbContext.Documents.Where(d => d.UserId == userId);

        query = sortBy?.ToLowerInvariant() switch
        {
            "filename"   => ascending ? query.OrderBy(d => d.FileName)   : query.OrderByDescending(d => d.FileName),
            "filesize"   => ascending ? query.OrderBy(d => d.FileSize)   : query.OrderByDescending(d => d.FileSize),
            "contentype" => ascending ? query.OrderBy(d => d.ContentType) : query.OrderByDescending(d => d.ContentType),
            _            => ascending ? query.OrderBy(d => d.UploadedAt) : query.OrderByDescending(d => d.UploadedAt),
        };

        return await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<int> CountByUserIdAsync(string userId)
    {
        return await _dbContext.Documents.CountAsync(d => d.UserId == userId);
    }
}

