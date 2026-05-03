using System;
using MediatR;

namespace DocumentService.Application.Queries.GetDocumentConversation;

public record GetDocumentConversationQuery(Guid DocumentId) : IRequest<GetDocumentConversationResponse>;
