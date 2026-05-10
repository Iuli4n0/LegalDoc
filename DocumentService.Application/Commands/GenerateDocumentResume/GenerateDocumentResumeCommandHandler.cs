using System;
using DocumentService.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DocumentService.Application.Commands.GenerateDocumentResume;

public partial class GenerateDocumentResumeCommandHandler
    : IRequestHandler<GenerateDocumentResumeCommand, GenerateDocumentResumeResponse>
{
    private const string DocumentNotFoundError = "Document with ID '{0}' not found.";
    private const string UnsupportedContentTypeError = "Content type '{0}' is not supported for resume generation. Supported types: PDF, DOCX, TXT.";
    private const string NoTextExtractedError = "No text could be extracted from the document.";

    private readonly IDocumentRepository _documentRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly ITextExtractionService _textExtractionService;
    private readonly IResumeGeneratorService _resumeGeneratorService;
    private readonly ILogger<GenerateDocumentResumeCommandHandler> _logger;

    public GenerateDocumentResumeCommandHandler(
        IDocumentRepository documentRepository,
        IFileStorageService fileStorageService,
        ITextExtractionService textExtractionService,
        IResumeGeneratorService resumeGeneratorService,
        ILogger<GenerateDocumentResumeCommandHandler> logger)
    {
        _documentRepository = documentRepository;
        _fileStorageService = fileStorageService;
        _textExtractionService = textExtractionService;
        _resumeGeneratorService = resumeGeneratorService;
        _logger = logger;
    }

    public async Task<GenerateDocumentResumeResponse> Handle(
        GenerateDocumentResumeCommand request, CancellationToken cancellationToken)
    {
        LogResumeStarted(_logger, request.DocumentId);

        var document = await _documentRepository.GetByIdAsync(request.DocumentId).ConfigureAwait(false);
        if (document is null)
        {
            throw new InvalidOperationException(string.Format(DocumentNotFoundError, request.DocumentId));
        }

        if (!_textExtractionService.SupportsContentType(document.ContentType))
        {
            throw new NotSupportedException(
                string.Format(UnsupportedContentTypeError, document.ContentType));
        }

        await using var fileStream = await _fileStorageService.DownloadFileAsync(document.S3Key).ConfigureAwait(false);
        var extractedText = await _textExtractionService.ExtractTextAsync(fileStream, document.ContentType).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(extractedText))
        {
            throw new InvalidOperationException(NoTextExtractedError);
        }

        var resumeResult = await _resumeGeneratorService.GenerateResumeAsync(extractedText, cancellationToken).ConfigureAwait(false);

        document.SetResume(resumeResult.Resume);
        await _documentRepository.UpdateAsync(document).ConfigureAwait(false);

        var charCount = extractedText.Length;
        LogResumeCompleted(_logger, request.DocumentId, charCount, resumeResult.ChunksProcessed);

        return new GenerateDocumentResumeResponse(
            document.Id,
            resumeResult.Resume,
            document.ResumeGeneratedAt ?? DateTime.UtcNow,
            resumeResult.ChunksProcessed
        );
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Starting resume generation for document {DocumentId}")]
    private static partial void LogResumeStarted(ILogger logger, Guid documentId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Resume generation completed for document {DocumentId}. Characters extracted: {CharCount}, Chunks processed: {ChunksProcessed}")]
    private static partial void LogResumeCompleted(ILogger logger, Guid documentId, int charCount, int chunksProcessed);
}
