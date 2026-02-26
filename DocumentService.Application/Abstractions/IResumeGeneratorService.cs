using System.Threading;
namespace DocumentService.Application.Abstractions;
public interface IResumeGeneratorService
{
    Task<ResumeResult> GenerateResumeAsync(string text, CancellationToken cancellationToken = default);
}

public record ResumeResult(string Resume, int ChunksProcessed);

