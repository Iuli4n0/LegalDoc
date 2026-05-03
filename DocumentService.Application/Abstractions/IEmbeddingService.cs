using System.Threading;
using System.Threading.Tasks;

namespace DocumentService.Application.Abstractions;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}
