using System;
using System.Collections.Generic;

namespace LegalDoc.Frontend.Models;

internal record AskQuestionRequest(string Question);

internal record AskQuestionResponse(
    string Answer,
    List<SourceChunkItem> SourceChunks,
    bool IsNewlyIndexed
);

internal record SourceChunkItem(int ChunkIndex, string Text, double Distance);

internal record IndexDocumentResponse(Guid DocumentId, int ChunksCreated, DateTime IndexedAt);

internal record GetDocumentConversationResponse(List<DocumentMessageDto> Messages);

internal record DocumentMessageDto(
    Guid Id,
    bool IsUser,
    string Text,
    string? SourcesJson,
    DateTime CreatedAt
);
