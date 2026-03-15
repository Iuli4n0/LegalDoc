using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DocumentService.Application.Abstractions;

public interface IClauseExtractorService
{
    Task<ClauseExtractionResult> ExtractClausesAsync(string text, CancellationToken cancellationToken = default);
}

public record ClauseExtractionResult(IReadOnlyList<string> Clauses, int ChunksProcessed);

