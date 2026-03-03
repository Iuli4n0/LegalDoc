using LegalDoc.Frontend.Models;

namespace LegalDoc.Frontend.Test.Models;

public class DocumentModelsTests
{
    [Fact]
    public void Given_ValidParameters_When_UploadDocumentResponseIsCreated_Then_ShouldStoreAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var uploadedAt = DateTime.UtcNow;

        // Act
        var response = new UploadDocumentResponse(
            id, "user-123", "contract.pdf", "application/pdf", "s3://bucket/key", 1024, uploadedAt);

        // Assert
        Assert.Equal(id, response.Id);
        Assert.Equal("user-123", response.UserId);
        Assert.Equal("contract.pdf", response.FileName);
        Assert.Equal("application/pdf", response.ContentType);
        Assert.Equal("s3://bucket/key", response.S3Key);
        Assert.Equal(1024, response.FileSize);
        Assert.Equal(uploadedAt, response.UploadedAt);
    }

    [Fact]
    public void Given_TwoUploadDocumentResponsesWithSameData_When_Compared_Then_ShouldBeEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var uploadedAt = DateTime.UtcNow;

        // Act
        var response1 = new UploadDocumentResponse(id, "user-123", "contract.pdf", "application/pdf", "s3://key", 1024, uploadedAt);
        var response2 = new UploadDocumentResponse(id, "user-123", "contract.pdf", "application/pdf", "s3://key", 1024, uploadedAt);

        // Assert
        Assert.Equal(response1, response2);
    }

    [Fact]
    public void Given_ValidParameters_When_GetDocumentResponseIsCreated_Then_ShouldStoreAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var uploadedAt = DateTime.UtcNow;
        var resumeGeneratedAt = DateTime.UtcNow.AddMinutes(5);

        // Act
        var response = new GetDocumentResponse(
            id, "user-123", "contract.pdf", "application/pdf", "s3://bucket/key",
            2048, uploadedAt, "Rezumat document", resumeGeneratedAt);

        // Assert
        Assert.Equal(id, response.Id);
        Assert.Equal("user-123", response.UserId);
        Assert.Equal("contract.pdf", response.FileName);
        Assert.Equal("application/pdf", response.ContentType);
        Assert.Equal("s3://bucket/key", response.S3Key);
        Assert.Equal(2048, response.FileSize);
        Assert.Equal(uploadedAt, response.UploadedAt);
        Assert.Equal("Rezumat document", response.Resume);
        Assert.Equal(resumeGeneratedAt, response.ResumeGeneratedAt);
    }

    [Fact]
    public void Given_NullResumeAndResumeGeneratedAt_When_GetDocumentResponseIsCreated_Then_ShouldAllowNullValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var uploadedAt = DateTime.UtcNow;

        // Act
        var response = new GetDocumentResponse(
            id, "user-123", "contract.pdf", "application/pdf", "s3://bucket/key",
            2048, uploadedAt, null, null);

        // Assert
        Assert.Null(response.Resume);
        Assert.Null(response.ResumeGeneratedAt);
    }

    [Fact]
    public void Given_ValidParameters_When_GenerateResumeResponseIsCreated_Then_ShouldStoreAllProperties()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var generatedAt = DateTime.UtcNow;

        // Act
        var response = new GenerateResumeResponse(documentId, "Rezumat generat de AI", generatedAt, 5);

        // Assert
        Assert.Equal(documentId, response.DocumentId);
        Assert.Equal("Rezumat generat de AI", response.Resume);
        Assert.Equal(generatedAt, response.GeneratedAt);
        Assert.Equal(5, response.ChunksProcessed);
    }

    [Fact]
    public void Given_TwoGenerateResumeResponsesWithSameData_When_Compared_Then_ShouldBeEqual()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var generatedAt = DateTime.UtcNow;

        // Act
        var response1 = new GenerateResumeResponse(documentId, "Rezumat", generatedAt, 3);
        var response2 = new GenerateResumeResponse(documentId, "Rezumat", generatedAt, 3);

        // Assert
        Assert.Equal(response1, response2);
    }

    [Fact]
    public void Given_TwoGetDocumentResponsesWithSameData_When_Compared_Then_ShouldBeEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var uploadedAt = DateTime.UtcNow;

        // Act
        var response1 = new GetDocumentResponse(id, "user-123", "file.pdf", "application/pdf", "s3://key", 1024, uploadedAt, null, null);
        var response2 = new GetDocumentResponse(id, "user-123", "file.pdf", "application/pdf", "s3://key", 1024, uploadedAt, null, null);

        // Assert
        Assert.Equal(response1, response2);
    }

    [Fact]
    public void Given_ZeroFileSize_When_UploadDocumentResponseIsCreated_Then_ShouldAcceptZero()
    {
        // Arrange
        var id = Guid.NewGuid();
        var uploadedAt = DateTime.UtcNow;

        // Act
        var response = new UploadDocumentResponse(id, "user-123", "empty.pdf", "application/pdf", "s3://key", 0, uploadedAt);

        // Assert
        Assert.Equal(0, response.FileSize);
    }

    [Fact]
    public void Given_ZeroChunksProcessed_When_GenerateResumeResponseIsCreated_Then_ShouldAcceptZero()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var generatedAt = DateTime.UtcNow;

        // Act
        var response = new GenerateResumeResponse(documentId, "Rezumat scurt", generatedAt, 0);

        // Assert
        Assert.Equal(0, response.ChunksProcessed);
    }
}

