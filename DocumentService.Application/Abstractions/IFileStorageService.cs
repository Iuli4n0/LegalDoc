using System.IO;
using System.Threading.Tasks;

namespace DocumentService.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);
}

