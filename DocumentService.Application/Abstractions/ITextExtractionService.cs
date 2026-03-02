using System.IO;
using System.Threading.Tasks;

namespace DocumentService.Application.Abstractions;

public interface ITextExtractionService
{
    Task<string> ExtractTextAsync(Stream fileStream, string contentType);
    bool SupportsContentType(string contentType);
}

