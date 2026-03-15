using System;

namespace DocumentService.Domain.Entities;

public class Clause
{
    private Clause()
    {
    }

    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public Document Document { get; private set; } = null!;
    public string Text { get; private set; } = null!;
    public DateTime ExtractedAt { get; private set; }

    public static Clause Create(Guid documentId, string text)
    {
        if (documentId == Guid.Empty)
            throw new ArgumentException("Document ID cannot be empty.", nameof(documentId));
        
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Clause text cannot be empty.", nameof(text));
        
        return new Clause
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            Text = text,
            ExtractedAt = DateTime.UtcNow
        };
    }
}
