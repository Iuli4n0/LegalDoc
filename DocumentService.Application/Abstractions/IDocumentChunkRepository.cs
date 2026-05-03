using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DocumentService.Domain.Entities;

namespace DocumentService.Application.Abstractions;

public interface IDocumentChunkRepository
{
    Task AddRangeAsync(IEnumerable<DocumentChunk> chunks);
    Task<List<DocumentChunkSearchResult>> SearchSimilarAsync(Guid documentId, float[] queryEmbedding, int topK = 5);
    Task<bool> IsDocumentIndexedAsync(Guid documentId);
    Task DeleteByDocumentIdAsync(Guid documentId);
    Task<int> CountByDocumentIdAsync(Guid documentId);
}

public record DocumentChunkSearchResult(Guid Id, int ChunkIndex, string Text, double Distance);
