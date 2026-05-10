using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DocumentService.Application.Commands.ClassifyDocumentClauses;

public partial class ClassifyDocumentClausesCommandHandler
    : IRequestHandler<ClassifyDocumentClausesCommand, ClassifyDocumentClausesResponse>
{
    private const string DocumentNotFoundError = "Document with ID '{0}' not found.";
    private const string NoClausesAvailableError = "No clauses found for classification.";

    private readonly IDocumentRepository _documentRepository;
    private readonly IClauseRepository _clauseRepository;
    private readonly IClauseClassificationService _clauseClassificationService;
    private readonly ILogger<ClassifyDocumentClausesCommandHandler> _logger;

    public ClassifyDocumentClausesCommandHandler(
        IDocumentRepository documentRepository,
        IClauseRepository clauseRepository,
        IClauseClassificationService clauseClassificationService,
        ILogger<ClassifyDocumentClausesCommandHandler> logger)
    {
        _documentRepository = documentRepository;
        _clauseRepository = clauseRepository;
        _clauseClassificationService = clauseClassificationService;
        _logger = logger;
    }

    public async Task<ClassifyDocumentClausesResponse> Handle(ClassifyDocumentClausesCommand request, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(request.DocumentId).ConfigureAwait(false);
        if (document is null)
        {
            throw new InvalidOperationException(string.Format(DocumentNotFoundError, request.DocumentId));
        }

        var clauses = await _clauseRepository.GetByDocumentIdAsync(request.DocumentId).ConfigureAwait(false);
        if (clauses.Count == 0)
        {
            throw new InvalidOperationException(NoClausesAvailableError);
        }

        foreach (var clause in clauses)
        {
            var result = await _clauseClassificationService.ClassifyAsync(clause.Text, cancellationToken).ConfigureAwait(false);
            clause.SetClassification(result.Label == 1, result.AbusiveProbability);
        }

        await _clauseRepository.UpdateRangeAsync(clauses, cancellationToken).ConfigureAwait(false);

        var classifiedAt = DateTime.UtcNow;
        LogClassificationCompleted(_logger, request.DocumentId, clauses.Count);

        return new ClassifyDocumentClausesResponse(
            request.DocumentId,
            clauses.Select(c => new ClassifiedClauseResponseItem(
                c.Id,
                c.Text,
                c.IsAbusive ?? false,
                c.AbusiveProbability ?? 0,
                c.ClassifiedAt ?? classifiedAt)).ToList(),
            classifiedAt);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Clause classification completed for document {DocumentId}. Clauses classified: {ClauseCount}")]
    private static partial void LogClassificationCompleted(ILogger logger, Guid documentId, int clauseCount);
}
