using Amazon.S3;
using Amazon.S3.Model;
using DocumentService.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace DocumentService.Test.Infrastructure.Services;

public class S3FileStorageServiceTests
{
    private readonly Mock<IAmazonS3> _s3Mock = new();
    private readonly Mock<ILogger<S3FileStorageService>> _loggerMock = new();

    [Fact]
    public void Given_MissingBucketName_When_ServiceIsConstructed_Then_InvalidOperationExceptionIsThrown()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new S3FileStorageService(_s3Mock.Object, config, _loggerMock.Object));

        Assert.Contains("AWS:BucketName", exception.Message);
    }

    [Fact]
    public async Task Given_ValidUpload_When_UploadFileAsync_Then_KeyIsReturned()
    {
        var service = CreateService();
        _s3Mock
            .Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse { ETag = "etag" });

        using var stream = new MemoryStream([1, 2, 3]);

        var key = await service.UploadFileAsync(stream, "doc.pdf", "application/pdf");

        Assert.EndsWith("/doc.pdf", key);
        _s3Mock.Verify(s => s.PutObjectAsync(
            It.Is<PutObjectRequest>(r => r.BucketName == "bucket" && r.ContentType == "application/pdf"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Given_AmazonS3Exception_When_UploadFileAsync_Then_InvalidOperationExceptionIsThrown()
    {
        var service = CreateService();
        _s3Mock
            .Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("aws") { ErrorCode = "AccessDenied" });

        using var stream = new MemoryStream([1]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadFileAsync(stream, "doc.pdf", "application/pdf"));

        Assert.Contains("Failed to upload file to S3", exception.Message);
    }

    [Fact]
    public async Task Given_UnexpectedException_When_UploadFileAsync_Then_InvalidOperationExceptionIsThrown()
    {
        var service = CreateService();
        _s3Mock
            .Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));

        using var stream = new MemoryStream([1]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadFileAsync(stream, "doc.pdf", "application/pdf"));

        Assert.Contains("Unexpected error uploading file", exception.Message);
    }

    [Fact]
    public async Task Given_ValidDownload_When_DownloadFileAsync_Then_StreamIsReturned()
    {
        var service = CreateService();
        _s3Mock
            .Setup(s => s.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetObjectResponse
            {
                ResponseStream = new MemoryStream([1, 2, 3, 4])
            });

        await using var result = await service.DownloadFileAsync("key");
        using var reader = new BinaryReader(result);

        var bytes = reader.ReadBytes(4);

        Assert.Equal([1, 2, 3, 4], bytes);
    }

    [Fact]
    public async Task Given_AmazonS3Exception_When_DownloadFileAsync_Then_InvalidOperationExceptionIsThrown()
    {
        var service = CreateService();
        _s3Mock
            .Setup(s => s.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AmazonS3Exception("aws") { ErrorCode = "NoSuchKey" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DownloadFileAsync("key"));

        Assert.Contains("Failed to download file from S3", exception.Message);
    }

    [Fact]
    public async Task Given_UnexpectedException_When_DownloadFileAsync_Then_InvalidOperationExceptionIsThrown()
    {
        var service = CreateService();
        _s3Mock
            .Setup(s => s.GetObjectAsync(It.IsAny<GetObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("boom"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DownloadFileAsync("key"));

        Assert.Contains("Unexpected error downloading file", exception.Message);
    }

    private S3FileStorageService CreateService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AWS:BucketName"] = "bucket"
            })
            .Build();

        return new S3FileStorageService(_s3Mock.Object, config, _loggerMock.Object);
    }
}

