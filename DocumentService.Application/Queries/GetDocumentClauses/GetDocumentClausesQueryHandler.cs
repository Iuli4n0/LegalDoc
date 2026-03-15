using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using MediatR;

namespace DocumentService.Application.Queries.GetDocumentClauses;

public class GetDocumentClausesQueryHandler : IRequestHandler<GetDocumentClausesQuery, GetDocumentClausesResponse>
{
    private readonly IClauseRepository _clauseRepository;
    private readonly IDocumentRepository _documentRepository;

    public GetDocumentClausesQueryHandler(IClauseRepository clauseRepository, IDocumentRepository documentRepository)
    {
        _clauseRepository = clauseRepository;
        _documentRepository = documentRepository;
    }

    public async Task<GetDocumentClausesResponse> Handle(GetDocumentClausesQuery request, CancellationToken cancellationToken)
    {
        var document = await _documentRepository.GetByIdAsync(request.DocumentId);
        if (document is null)
        {
            throw new InvalidOperationException($"Document with ID '{request.DocumentId}' not found.");
        }

        var clauses = await _clauseRepository.GetByDocumentIdAsync(request.DocumentId);

        return new GetDocumentClausesResponse(
            request.DocumentId,
            clauses.Select(c => c.Text).ToList(),
            clauses.Count > 0 ? clauses.Max(c => c.ExtractedAt) : null
        );
    }
}
