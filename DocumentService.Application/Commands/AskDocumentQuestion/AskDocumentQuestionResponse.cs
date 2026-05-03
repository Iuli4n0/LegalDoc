using System;
using System.Collections.Generic;

namespace DocumentService.Application.Commands.AskDocumentQuestion;

public record AskDocumentQuestionResponse(
    string Answer,
    List<SourceChunkDto> SourceChunks,
    bool IsNewlyIndexed
);

public record SourceChunkDto(int ChunkIndex, string Text, double Distance);
