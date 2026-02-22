using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using DocumentService.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DocumentService.Infrastructure.Services;

public class S3FileStorageService : IFileStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<S3FileStorageService> _logger;

    public S3FileStorageService(
        IAmazonS3 s3Client, 
        IConfiguration configuration,
        ILogger<S3FileStorageService> logger)
    {
        _s3Client = s3Client;
        _logger = logger;
        _bucketName = configuration["AWS:BucketName"]
                      ?? throw new InvalidOperationException("AWS:BucketName is not configured.");
        
        _logger.LogInformation("S3FileStorageService initialized with bucket: {BucketName}", _bucketName);
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
    {
        try
        {
            var key = $"{Guid.NewGuid()}/{fileName}";

            _logger.LogInformation("Uploading file {FileName} to S3 with key {Key}", fileName, key);

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = fileStream,
                ContentType = contentType
            };

            var response = await _s3Client.PutObjectAsync(request);

            _logger.LogInformation(
                "Successfully uploaded file {FileName} to S3. Key: {Key}, ETag: {ETag}", 
                fileName, 
                key, 
                response.ETag);

            return key;
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(ex, 
                "AWS S3 error uploading file {FileName}. Error Code: {ErrorCode}, Message: {Message}", 
                fileName, 
                ex.ErrorCode, 
                ex.Message);
            throw new InvalidOperationException($"Failed to upload file to S3: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error uploading file {FileName} to S3", fileName);
            throw new InvalidOperationException($"Unexpected error uploading file: {ex.Message}", ex);
        }
    }
}

