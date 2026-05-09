using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DocumentService.Application.Queries.GetDocumentConversation;

public class GetDocumentConversationQueryHandler : IRequestHandler<GetDocumentConversationQuery, GetDocumentConversationResponse>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentMessageRepository _messageRepository;
    private readonly ILogger<GetDocumentConversationQueryHandler> _logger;

    public GetDocumentConversationQueryHandler(
        IDocumentRepository documentRepository,
        IDocumentMessageRepository messageRepository,
        ILogger<GetDocumentConversationQueryHandler> logger)
    {
        _documentRepository = documentRepository;
        _messageRepository = messageRepository;
        _logger = logger;
    }

    public async Task<GetDocumentConversationResponse> Handle(GetDocumentConversationQuery request, CancellationToken cancellationToken)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Retrieving conversation for document {DocumentId}", request.DocumentId);
        }

        var document = await _documentRepository.GetByIdAsync(request.DocumentId).ConfigureAwait(false);
        if (document is null)
            throw new InvalidOperationException($"Document with ID '{request.DocumentId}' not found.");

        var messages = await _messageRepository.GetByDocumentIdAsync(request.DocumentId).ConfigureAwait(false);

        var dtos = messages.Select(m => new DocumentMessageDto(
            m.Id,
            m.IsUser,
            m.Text,
            m.SourcesJson,
            m.CreatedAt
        )).ToList();

        return new GetDocumentConversationResponse(dtos);
    }
}
