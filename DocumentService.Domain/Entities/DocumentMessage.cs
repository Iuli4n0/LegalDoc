using System;

namespace DocumentService.Domain.Entities;

public class DocumentMessage
{
    private DocumentMessage() { }

    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public bool IsUser { get; private set; }
    public string Text { get; private set; } = null!;
    public string? SourcesJson { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public Document Document { get; private set; } = null!;

    public static DocumentMessage Create(Guid documentId, bool isUser, string text, string? sourcesJson = null)
    {
        if (documentId == Guid.Empty)
            throw new ArgumentException("Document ID cannot be empty.", nameof(documentId));

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty.", nameof(text));

        return new DocumentMessage
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            IsUser = isUser,
            Text = text,
            SourcesJson = sourcesJson,
            CreatedAt = DateTime.UtcNow
        };
    }
}
