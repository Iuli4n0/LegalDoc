using System;
using System.Collections.Generic;

namespace DocumentService.Application.Queries.GetDocumentConversation;

public record GetDocumentConversationResponse(List<DocumentMessageDto> Messages);

public record DocumentMessageDto(
    Guid Id,
    bool IsUser,
    string Text,
    string? SourcesJson,
    DateTime CreatedAt
);
