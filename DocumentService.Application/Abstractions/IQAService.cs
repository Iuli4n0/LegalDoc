using System.Threading;
using System.Threading.Tasks;

namespace DocumentService.Application.Abstractions;

public interface IQAService
{
    Task<string> GenerateAnswerAsync(string question, string[] contextChunks, CancellationToken cancellationToken = default);
}
