using System;
using System.Linq;
using DocumentService.Application.Abstractions;
using DocumentService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DocumentService.Application.Commands.GenerateDocumentClauses;

public class GenerateDocumentClausesCommandHandler
    : IRequestHandler<GenerateDocumentClausesCommand, GenerateDocumentClausesResponse>
{
    private const string DocumentNotFoundError = "Document with ID '{0}' not found.";
    private const string UnsupportedContentTypeError = "Content type '{0}' is not supported for clause extraction. Supported types: PDF, DOCX, TXT.";
    private const string NoTextExtractedError = "No text could be extracted from the document.";
    private const string NoClausesExtractedError = "No clauses could be extracted from the document.";
    private readonly IDocumentRepository _documentRepository;
    private readonly IClauseRepository _clauseRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ITextExtractionService _textExtractionService;
    private readonly IClauseExtractorService _clauseExtractorService;
    private readonly ILogger<GenerateDocumentClausesCommandHandler> _logger;

    public GenerateDocumentClausesCommandHandler(
        IDocumentRepository documentRepository,
        IClauseRepository clauseRepository,
        IFileStorageService fileStorageService,
        ITextExtractionService textExtractionService,
        IClauseExtractorService clauseExtractorService,
        ILogger<GenerateDocumentClausesCommandHandler> logger)
    {
        _documentRepository = documentRepository;
        _clauseRepository = clauseRepository;
        _fileStorageService = fileStorageService;
        _textExtractionService = textExtractionService;
        _clauseExtractorService = clauseExtractorService;
        _logger = logger;
    }

    public async Task<GenerateDocumentClausesResponse> Handle(
        GenerateDocumentClausesCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting clause extraction for document {DocumentId}", request.DocumentId);

        var document = await _documentRepository.GetByIdAsync(request.DocumentId);
        if (document is null)
        {
            throw new InvalidOperationException(string.Format(DocumentNotFoundError, request.DocumentId));
        }

        if (!_textExtractionService.SupportsContentType(document.ContentType))
        {
            throw new NotSupportedException(
                string.Format(UnsupportedContentTypeError, document.ContentType));
        }

        await using var fileStream = await _fileStorageService.DownloadFileAsync(document.S3Key);
        var extractedText = await _textExtractionService.ExtractTextAsync(fileStream, document.ContentType);

        if (string.IsNullOrWhiteSpace(extractedText))
        {
            throw new InvalidOperationException(NoTextExtractedError);
        }

        var extractionResult = await _clauseExtractorService.ExtractClausesAsync(extractedText, cancellationToken);
        if (extractionResult.Clauses.Count == 0)
        {
            throw new InvalidOperationException(NoClausesExtractedError);
        }

        var clausesToSave = extractionResult.Clauses
            .Select(text => Clause.Create(document.Id, text))
            .ToList();

        await _clauseRepository.AddRangeAsync(clausesToSave);

        var generatedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "Clause extraction completed for document {DocumentId}. Characters extracted: {CharCount}, Clauses extracted: {ClauseCount}, Chunks processed: {ChunksProcessed}",
            request.DocumentId, extractedText.Length, extractionResult.Clauses.Count, extractionResult.ChunksProcessed);

        return new GenerateDocumentClausesResponse(
            document.Id,
            extractionResult.Clauses,
            generatedAt,
            extractionResult.ChunksProcessed
        );
    }
}

