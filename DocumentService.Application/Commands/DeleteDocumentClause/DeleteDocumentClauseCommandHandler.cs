using System;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using MediatR;

namespace DocumentService.Application.Commands.DeleteDocumentClause;

public class DeleteDocumentClauseCommandHandler : IRequestHandler<DeleteDocumentClauseCommand>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IClauseRepository _clauseRepository;

    public DeleteDocumentClauseCommandHandler(IDocumentRepository documentRepository, IClauseRepository clauseRepository)
    {
        _documentRepository = documentRepository;
        _clauseRepository = clauseRepository;
    }

    public async Task Handle(DeleteDocumentClauseCommand request, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(request.DocumentId).ConfigureAwait(false);
        if (document is null)
            throw new InvalidOperationException("Document not found.");

        if (document.UserId != request.UserId)
            throw new InvalidOperationException("Document not found.");

        var clause = await _clauseRepository.GetByIdAsync(request.ClauseId, cancellationToken).ConfigureAwait(false);
        if (clause is null || clause.DocumentId != request.DocumentId)
            throw new InvalidOperationException("Clause not found.");

        await _clauseRepository.DeleteAsync(clause, cancellationToken).ConfigureAwait(false);
    }
}
