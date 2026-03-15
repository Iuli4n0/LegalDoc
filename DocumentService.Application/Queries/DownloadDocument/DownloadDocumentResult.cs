using System.IO;

namespace DocumentService.Application.Queries.DownloadDocument;

public record DownloadDocumentResult(Stream Stream, string ContentType, string FileName);

