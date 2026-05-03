using System;

namespace DocumentService.Application.Commands.IndexDocument;

public record IndexDocumentResponse(Guid DocumentId, int ChunksCreated, DateTime IndexedAt);
