using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using DocumentService.Application.Commands.IndexDocument;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DocumentService.Application.Commands.AskDocumentQuestion;

public partial class AskDocumentQuestionCommandHandler
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
        LogProcessingQA(_logger, request.DocumentId, request.Question);

        // Save user message
        var userMessage = DocumentService.Domain.Entities.DocumentMessage.Create(
            request.DocumentId, true, request.Question);
        await _messageRepository.AddAsync(userMessage).ConfigureAwait(false);

        // Verify document exists
        var document = await _documentRepository.GetByIdAsync(request.DocumentId).ConfigureAwait(false);
        if (document is null)
            throw new InvalidOperationException($"Document with ID '{request.DocumentId}' not found.");

        // Check if document is indexed; if not, index it first
        var isNewlyIndexed = false;
        var isIndexed = await _chunkRepository.IsDocumentIndexedAsync(request.DocumentId).ConfigureAwait(false);
        if (!isIndexed)
        {
            LogDocumentNotIndexed(_logger, request.DocumentId);
            await _mediator.Send(new IndexDocumentCommand(request.DocumentId), cancellationToken).ConfigureAwait(false);
            isNewlyIndexed = true;
        }

        // Generate embedding for the question
        _logger.LogDebug("Generating embedding for question");
        var questionEmbedding = await _embeddingService.GenerateEmbeddingAsync(request.Question, cancellationToken).ConfigureAwait(false);

        // Search for similar chunks
        var searchResults = await _chunkRepository.SearchSimilarAsync(
            request.DocumentId, questionEmbedding, TopK).ConfigureAwait(false);

        if (searchResults.Count == 0)
        {
            _logger.LogWarning("No relevant chunks found for document {DocumentId}", request.DocumentId);
            return new AskDocumentQuestionResponse(
                "Nu am găsit fragmente relevante în document pentru a răspunde la această întrebare.",
                [],
                isNewlyIndexed);
        }

        LogFoundChunks(_logger, searchResults.Count);

        // Generate answer using LLM
        var contextChunks = searchResults.Select(r => r.Text).ToArray();
        var answer = await _qaService.GenerateAnswerAsync(request.Question, contextChunks, cancellationToken).ConfigureAwait(false);

        var sourceChunks = searchResults
            .Select(r => new SourceChunkDto(r.ChunkIndex, r.Text, r.Distance))
            .ToList();

        // Save assistant message
        var sourcesJson = System.Text.Json.JsonSerializer.Serialize(sourceChunks);
        var assistantMessage = DocumentService.Domain.Entities.DocumentMessage.Create(
            request.DocumentId, false, answer, sourcesJson);
        await _messageRepository.AddAsync(assistantMessage).ConfigureAwait(false);

        return new AskDocumentQuestionResponse(answer, sourceChunks, isNewlyIndexed);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Processing Q&A for document {DocumentId}: \"{Question}\"")]
    private static partial void LogProcessingQA(ILogger logger, Guid documentId, string question);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Document {DocumentId} is not indexed. Starting indexing...")]
    private static partial void LogDocumentNotIndexed(ILogger logger, Guid documentId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Found {Count} relevant chunks")]
    private static partial void LogFoundChunks(ILogger logger, int count);
}
