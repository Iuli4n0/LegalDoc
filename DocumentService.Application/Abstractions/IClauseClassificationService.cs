using System.Threading;
using System.Threading.Tasks;

namespace DocumentService.Application.Abstractions;

public interface IClauseClassificationService
{
    Task<ClauseClassificationResult> ClassifyAsync(string clauseText, CancellationToken cancellationToken = default);
}

public record ClauseClassificationResult(int Label, double AbusiveProbability);

