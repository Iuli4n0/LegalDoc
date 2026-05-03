using System;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using DocumentService.Domain.Entities;
using MediatR;

namespace DocumentService.Application.Commands.AddDocumentClause;

public class AddDocumentClauseCommandHandler : IRequestHandler<AddDocumentClauseCommand, AddDocumentClauseResponse>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IClauseRepository _clauseRepository;

    public AddDocumentClauseCommandHandler(IDocumentRepository documentRepository, IClauseRepository clauseRepository)
    {
        _documentRepository = documentRepository;
        _clauseRepository = clauseRepository;
    }

    public async Task<AddDocumentClauseResponse> Handle(AddDocumentClauseCommand request, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(request.DocumentId);
        if (document is null)
            throw new InvalidOperationException("Document not found.");

        if (document.UserId != request.UserId)
            throw new InvalidOperationException("Document not found.");

        var clause = Clause.Create(request.DocumentId, request.Text);
        await _clauseRepository.AddAsync(clause);

        return new AddDocumentClauseResponse(
            clause.Id,
            clause.Text,
            clause.IsAbusive,
            clause.AbusiveProbability,
            clause.ClassifiedAt
        );
    }
}
