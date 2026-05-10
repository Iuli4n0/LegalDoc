using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using DocumentService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DocumentService.Application.Commands.IndexDocument;

public partial class IndexDocumentCommandHandler
    : IRequestHandler<IndexDocumentCommand, IndexDocumentResponse>
{
    private const int DefaultChunkSize = 1200;
    private const int ChunkOverlap = 200;
    private const double ChunkSplitThreshold = 0.15;

    private readonly IDocumentRepository _documentRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ITextExtractionService _textExtractionService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentChunkRepository _chunkRepository;
    private readonly ILogger<IndexDocumentCommandHandler> _logger;

    public IndexDocumentCommandHandler(
        IDocumentRepository documentRepository,
        IFileStorageService fileStorageService,
        ITextExtractionService textExtractionService,
        IEmbeddingService embeddingService,
        IDocumentChunkRepository chunkRepository,
        ILogger<IndexDocumentCommandHandler> logger)
    {
        _documentRepository = documentRepository;
        _fileStorageService = fileStorageService;
        _textExtractionService = textExtractionService;
        _embeddingService = embeddingService;
        _chunkRepository = chunkRepository;
        _logger = logger;
    }

    public async Task<IndexDocumentResponse> Handle(
        IndexDocumentCommand request, CancellationToken cancellationToken)
    {
        LogIndexingStarted(_logger, request.DocumentId);

        var document = await _documentRepository.GetByIdAsync(request.DocumentId).ConfigureAwait(false);
        if (document is null)
            throw new InvalidOperationException($"Document with ID '{request.DocumentId}' not found.");

        if (!_textExtractionService.SupportsContentType(document.ContentType))
            throw new NotSupportedException(
                $"Content type '{document.ContentType}' is not supported for indexing. Supported types: PDF, DOCX, TXT.");

        // Delete any existing chunks (re-index scenario)
        await _chunkRepository.DeleteByDocumentIdAsync(request.DocumentId).ConfigureAwait(false);

        // Extract text from document
        await using var fileStream = await _fileStorageService.DownloadFileAsync(document.S3Key).ConfigureAwait(false);
        var extractedText = await _textExtractionService.ExtractTextAsync(fileStream, document.ContentType).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(extractedText))
            throw new InvalidOperationException("No text could be extracted from the document.");

        // Split into chunks
        var textChunks = SplitIntoChunks(extractedText, DefaultChunkSize, ChunkOverlap);
        LogTextSplit(_logger, textChunks.Count);

        // Generate embeddings and create entities
        var documentChunks = new List<DocumentChunk>();
        for (var i = 0; i < textChunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LogGeneratingEmbedding(_logger, i + 1, textChunks.Count);
            var embedding = await _embeddingService.GenerateEmbeddingAsync(textChunks[i], cancellationToken).ConfigureAwait(false);
            var chunk = DocumentChunk.Create(request.DocumentId, i, textChunks[i], embedding);
            documentChunks.Add(chunk);
        }

        // Store all chunks
        await _chunkRepository.AddRangeAsync(documentChunks).ConfigureAwait(false);

        var indexedAt = DateTime.UtcNow;
        LogIndexingCompleted(_logger, request.DocumentId, documentChunks.Count);

        return new IndexDocumentResponse(request.DocumentId, documentChunks.Count, indexedAt);
    }

    public static List<string> SplitIntoChunks(string text, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        var currentIndex = 0;

        while (currentIndex < text.Length)
        {
            var remainingLength = text.Length - currentIndex;
            var length = Math.Min(chunkSize, remainingLength);

            if (length >= remainingLength)
            {
                var lastChunk = text.Substring(currentIndex, length).Trim();
                if (lastChunk.Length > 0)
                    chunks.Add(lastChunk);
                break;
            }

            var chunk = text.Substring(currentIndex, length);
            var cutPoint = FindCutPoint(chunk, chunkSize);

            if (cutPoint > 0)
            {
                chunk = chunk[..cutPoint];
                length = cutPoint;
            }

            var trimmedChunk = chunk.Trim();
            if (trimmedChunk.Length > 0)
                chunks.Add(trimmedChunk);

            // Move forward with overlap
            currentIndex += Math.Max(1, length - overlap);
        }

        return chunks;
    }

    private static int FindCutPoint(string chunk, int chunkSize)
    {
        var splitThreshold = chunkSize * ChunkSplitThreshold;
        var separators = new (int index, int offset)[]
        {
            (chunk.LastIndexOf("\n\n", StringComparison.Ordinal), 2),
            (chunk.LastIndexOf('\n'), 1),
            (chunk.LastIndexOf(". ", StringComparison.Ordinal), 2),
            (chunk.LastIndexOf(' '), 0)
        };

        foreach (var (index, offset) in separators)
        {
            if (index > splitThreshold)
                return index + offset;
        }

        return -1;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Starting document indexing for document {DocumentId}")]
    private static partial void LogIndexingStarted(ILogger logger, Guid documentId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Text split into {ChunkCount} chunk(s) for indexing")]
    private static partial void LogTextSplit(ILogger logger, int chunkCount);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "Generating embedding for chunk {Current}/{Total}")]
    private static partial void LogGeneratingEmbedding(ILogger logger, int current, int total);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Document indexing completed for {DocumentId}. {ChunkCount} chunks created.")]
    private static partial void LogIndexingCompleted(ILogger logger, Guid documentId, int chunkCount);
}
