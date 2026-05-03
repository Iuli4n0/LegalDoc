using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using DocumentService.Application.Commands.IndexDocument;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DocumentService.Application.Commands.AskDocumentQuestion;

public class AskDocumentQuestionCommandHandler
    : IRequestHandler<AskDocumentQuestionCommand, AskDocumentQuestionResponse>
{
    private const int TopK = 8;

    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentChunkRepository _chunkRepository;
    private readonly IDocumentMessageRepository _messageRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IQAService _qaService;
    private readonly IMediator _mediator;
    private readonly ILogger<AskDocumentQuestionCommandHandler> _logger;

    public AskDocumentQuestionCommandHandler(
        IDocumentRepository documentRepository,
        IDocumentChunkRepository chunkRepository,
        IDocumentMessageRepository messageRepository,
        IEmbeddingService embeddingService,
        IQAService qaService,
        IMediator mediator,
        ILogger<AskDocumentQuestionCommandHandler> logger)
    {
        _documentRepository = documentRepository;
        _chunkRepository = chunkRepository;
        _messageRepository = messageRepository;
        _embeddingService = embeddingService;
        _qaService = qaService;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<AskDocumentQuestionResponse> Handle(
        AskDocumentQuestionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing Q&A for document {DocumentId}: \"{Question}\"",
            request.DocumentId, request.Question);

        // Save user message
        var userMessage = DocumentService.Domain.Entities.DocumentMessage.Create(
            request.DocumentId, true, request.Question);
        await _messageRepository.AddAsync(userMessage);

        // Verify document exists
        var document = await _documentRepository.GetByIdAsync(request.DocumentId);
        if (document is null)
            throw new InvalidOperationException($"Document with ID '{request.DocumentId}' not found.");

        // Check if document is indexed; if not, index it first
        var isNewlyIndexed = false;
        var isIndexed = await _chunkRepository.IsDocumentIndexedAsync(request.DocumentId);
        if (!isIndexed)
        {
            _logger.LogInformation("Document {DocumentId} is not indexed. Starting indexing...", request.DocumentId);
            await _mediator.Send(new IndexDocumentCommand(request.DocumentId), cancellationToken);
            isNewlyIndexed = true;
        }

        // Generate embedding for the question
        _logger.LogInformation("Generating embedding for question");
        var questionEmbedding = await _embeddingService.GenerateEmbeddingAsync(request.Question, cancellationToken);

        // Search for similar chunks
        var searchResults = await _chunkRepository.SearchSimilarAsync(
            request.DocumentId, questionEmbedding, TopK);

        if (searchResults.Count == 0)
        {
            _logger.LogWarning("No relevant chunks found for document {DocumentId}", request.DocumentId);
            return new AskDocumentQuestionResponse(
                "Nu am găsit fragmente relevante în document pentru a răspunde la această întrebare.",
                [],
                isNewlyIndexed);
        }

        _logger.LogInformation("Found {Count} relevant chunks", searchResults.Count);

        // Generate answer using LLM
        var contextChunks = searchResults.Select(r => r.Text).ToArray();
        var answer = await _qaService.GenerateAnswerAsync(request.Question, contextChunks, cancellationToken);

        var sourceChunks = searchResults
            .Select(r => new SourceChunkDto(r.ChunkIndex, r.Text, r.Distance))
            .ToList();

        // Save assistant message
        var sourcesJson = System.Text.Json.JsonSerializer.Serialize(sourceChunks);
        var assistantMessage = DocumentService.Domain.Entities.DocumentMessage.Create(
            request.DocumentId, false, answer, sourcesJson);
        await _messageRepository.AddAsync(assistantMessage);

        return new AskDocumentQuestionResponse(answer, sourceChunks, isNewlyIndexed);
    }
}
