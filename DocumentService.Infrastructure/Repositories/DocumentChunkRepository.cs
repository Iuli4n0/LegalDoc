using DocumentService.Application.Abstractions;
using DocumentService.Domain.Entities;
using DocumentService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace DocumentService.Infrastructure.Repositories;

public partial class DocumentChunkRepository : IDocumentChunkRepository
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

        if (_logger.IsEnabled(LogLevel.Information))
        {
            LogSimilaritySearch(_logger, documentId, results.Count, topK);
        }

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
            if (_logger.IsEnabled(LogLevel.Information))
            {
                LogDeletedChunks(_logger, chunks.Count, documentId);
            }
        }
    }

    public async Task<int> CountByDocumentIdAsync(Guid documentId)
    {
        return await _context.DocumentChunks
            .CountAsync(c => c.DocumentId == documentId).ConfigureAwait(false);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Similarity search for document {DocumentId}: found {Count} results (top-{TopK})")]
    private static partial void LogSimilaritySearch(ILogger logger, Guid documentId, int count, int topK);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Deleted {Count} chunks for document {DocumentId}")]
    private static partial void LogDeletedChunks(ILogger logger, int count, Guid documentId);
}
