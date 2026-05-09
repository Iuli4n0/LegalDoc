using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using DocumentService.Domain.Entities;
using DocumentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace DocumentService.Infrastructure.Repositories;

public class DocumentChunkRepository : IDocumentChunkRepository
{
    private readonly AppDbContext _context;
    private readonly ILogger<DocumentChunkRepository> _logger;

    public DocumentChunkRepository(AppDbContext context, ILogger<DocumentChunkRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AddRangeAsync(IEnumerable<DocumentChunk> chunks)
    {
        await _context.DocumentChunks.AddRangeAsync(chunks).ConfigureAwait(false);
        await _context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<List<DocumentChunkSearchResult>> SearchSimilarAsync(
        Guid documentId, float[] queryEmbedding, int topK = 5)
    {
        var queryVector = new Vector(queryEmbedding);

        var results = await _context.DocumentChunks
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.Embedding!.CosineDistance(queryVector))
            .Take(topK)
            .Select(c => new DocumentChunkSearchResult(
                c.Id,
                c.ChunkIndex,
                c.Text,
                c.Embedding!.CosineDistance(queryVector)))
            .ToListAsync().ConfigureAwait(false);

        _logger.LogInformation(
            "Similarity search for document {DocumentId}: found {Count} results (top-{TopK})",
            documentId, results.Count, topK);

        return results;
    }

    public async Task<bool> IsDocumentIndexedAsync(Guid documentId)
    {
        return await _context.DocumentChunks
            .AnyAsync(c => c.DocumentId == documentId).ConfigureAwait(false);
    }

    public async Task DeleteByDocumentIdAsync(Guid documentId)
    {
        var chunks = await _context.DocumentChunks
            .Where(c => c.DocumentId == documentId)
            .ToListAsync().ConfigureAwait(false);

        if (chunks.Count > 0)
        {
            _context.DocumentChunks.RemoveRange(chunks);
            await _context.SaveChangesAsync().ConfigureAwait(false);
            _logger.LogInformation("Deleted {Count} chunks for document {DocumentId}", chunks.Count, documentId);
        }
    }

    public async Task<int> CountByDocumentIdAsync(Guid documentId)
    {
        return await _context.DocumentChunks
            .CountAsync(c => c.DocumentId == documentId).ConfigureAwait(false);
    }
}
