using System;
using System.Collections.Generic;

namespace LegalDoc.Frontend.Models;

public record AskQuestionRequest(string Question);

public record AskQuestionResponse(
    string Answer,
    List<SourceChunkItem> SourceChunks,
    bool IsNewlyIndexed
);

public record SourceChunkItem(int ChunkIndex, string Text, double Distance);

public record IndexDocumentResponse(Guid DocumentId, int ChunksCreated, DateTime IndexedAt);

public record GetDocumentConversationResponse(List<DocumentMessageDto> Messages);

public record DocumentMessageDto(
    Guid Id,
    bool IsUser,
    string Text,
    string? SourcesJson,
    DateTime CreatedAt
);
