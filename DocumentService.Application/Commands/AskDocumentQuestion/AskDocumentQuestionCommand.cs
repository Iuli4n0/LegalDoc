using System;
using MediatR;

namespace DocumentService.Application.Commands.AskDocumentQuestion;

public record AskDocumentQuestionCommand(Guid DocumentId, string Question) : IRequest<AskDocumentQuestionResponse>;
