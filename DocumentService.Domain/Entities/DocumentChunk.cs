using System;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace DocumentService.Domain.Entities;

public class DocumentChunk
{
    private DocumentChunk()
    {
    }

    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public Document Document { get; private set; } = null!;
    public int ChunkIndex { get; private set; }
    public string Text { get; private set; } = null!;

    [Column(TypeName = "vector(768)")]
    public Vector? Embedding { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public static DocumentChunk Create(Guid documentId, int chunkIndex, string text, float[] embedding)
    {
        if (documentId == Guid.Empty)
            throw new ArgumentException("Document ID cannot be empty.", nameof(documentId));

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Chunk text cannot be empty.", nameof(text));

        if (embedding is null || embedding.Length == 0)
            throw new ArgumentException("Embedding cannot be empty.", nameof(embedding));

        return new DocumentChunk
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            Text = text,
            Embedding = new Vector(embedding),
            CreatedAt = DateTime.UtcNow
        };
    }
}
