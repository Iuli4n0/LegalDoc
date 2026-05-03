using System;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using DocumentService.Domain.Entities;
using MediatR;

namespace DocumentService.Application.Commands.MergeDocumentClauses;

public class MergeDocumentClausesCommandHandler : IRequestHandler<MergeDocumentClausesCommand, MergeDocumentClausesResponse>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IClauseRepository _clauseRepository;

    public MergeDocumentClausesCommandHandler(IDocumentRepository documentRepository, IClauseRepository clauseRepository)
    {
        _documentRepository = documentRepository;
        _clauseRepository = clauseRepository;
    }

    public async Task<MergeDocumentClausesResponse> Handle(MergeDocumentClausesCommand request, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(request.DocumentId);
        if (document is null)
            throw new InvalidOperationException("Document not found.");

        if (document.UserId != request.UserId)
            throw new InvalidOperationException("Document not found.");

        var clause1 = await _clauseRepository.GetByIdAsync(request.FirstClauseId, cancellationToken);
        var clause2 = await _clauseRepository.GetByIdAsync(request.SecondClauseId, cancellationToken);

        if (clause1 is null || clause1.DocumentId != request.DocumentId)
            throw new InvalidOperationException("First clause not found.");
            
        if (clause2 is null || clause2.DocumentId != request.DocumentId)
            throw new InvalidOperationException("Second clause not found.");

        // Create new merged clause
        var mergedText = $"{clause1.Text}\n\n{clause2.Text}";
        var newClause = Clause.Create(request.DocumentId, mergedText);

        await _clauseRepository.AddAsync(newClause);
        await _clauseRepository.DeleteAsync(clause1, cancellationToken);
        await _clauseRepository.DeleteAsync(clause2, cancellationToken);

        return new MergeDocumentClausesResponse(
            newClause.Id,
            newClause.Text,
            newClause.IsAbusive,
            newClause.AbusiveProbability,
            newClause.ClassifiedAt
        );
    }
}
