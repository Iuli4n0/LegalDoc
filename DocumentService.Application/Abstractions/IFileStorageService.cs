using System.IO;
using System.Threading.Tasks;

namespace DocumentService.Application.Abstractions;

public interface IFileStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);
    Task<Stream> DownloadFileAsync(string s3Key);
    Task DeleteFileAsync(string s3Key);
}

