using DocumentService.Domain.Entities;

namespace DocumentService.Test.Domain;

public class DocumentTests
{
    [Fact]
    public void Given_ValidParameters_When_CreateIsCalled_Then_DocumentIsCreatedWithCorrectProperties()
    {
        // Arrange
        var fileName = "contract.pdf";
        var contentType = "application/pdf";
        var s3Key = "documents/contract.pdf";
        var fileSize = 1024L;
        var userId = "user-123";

        // Act
        var document = Document.Create(fileName, contentType, s3Key, fileSize, userId);

        // Assert
        Assert.NotEqual(Guid.Empty, document.Id);
        Assert.Equal(fileName, document.FileName);
        Assert.Equal(contentType, document.ContentType);
        Assert.Equal(s3Key, document.S3Key);
        Assert.Equal(fileSize, document.FileSize);
        Assert.Equal(userId, document.UserId);
        Assert.True(document.UploadedAt <= DateTime.UtcNow);
        Assert.Null(document.Resume);
        Assert.Null(document.ResumeGeneratedAt);
    }

    [Fact]
    public void Given_NewDocument_When_CreateIsCalled_Then_UniqueIdIsGenerated()
    {
        // Arrange & Act
        var document1 = Document.Create("file1.pdf", "application/pdf", "key1", 100, "user-1");
        var document2 = Document.Create("file2.pdf", "application/pdf", "key2", 200, "user-2");

        // Assert
        Assert.NotEqual(document1.Id, document2.Id);
    }

    [Fact]
    public void Given_DocumentWithoutResume_When_SetResumeIsCalled_Then_ResumeAndTimestampAreSet()
    {
        // Arrange
        var document = Document.Create("contract.pdf", "application/pdf", "key", 1024, "user-123");
        var resumeText = "This is a summary of the legal document.";

        // Act
        document.SetResume(resumeText);

        // Assert
        Assert.Equal(resumeText, document.Resume);
        Assert.NotNull(document.ResumeGeneratedAt);
        Assert.True(document.ResumeGeneratedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Given_DocumentWithExistingResume_When_SetResumeIsCalledAgain_Then_ResumeIsOverwritten()
    {
        // Arrange
        var document = Document.Create("contract.pdf", "application/pdf", "key", 1024, "user-123");
        document.SetResume("Old resume");
        var newResume = "Updated resume content.";

        // Act
        document.SetResume(newResume);

        // Assert
        Assert.Equal(newResume, document.Resume);
    }

    [Fact]
    public void Given_ValidParameters_When_CreateIsCalled_Then_UploadedAtIsSetToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var document = Document.Create("file.pdf", "application/pdf", "key", 500, "user-1");

        // Assert
        var after = DateTime.UtcNow;
        Assert.InRange(document.UploadedAt, before, after);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_InvalidFileName_When_CreateIsCalled_Then_ArgumentExceptionIsThrown(string? fileName)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Document.Create(fileName!, "application/pdf", "key", 100, "user-1"));

        Assert.Equal("fileName", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_InvalidContentType_When_CreateIsCalled_Then_ArgumentExceptionIsThrown(string? contentType)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Document.Create("file.pdf", contentType!, "key", 100, "user-1"));

        Assert.Equal("contentType", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_InvalidS3Key_When_CreateIsCalled_Then_ArgumentExceptionIsThrown(string? s3Key)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Document.Create("file.pdf", "application/pdf", s3Key!, 100, "user-1"));

        Assert.Equal("s3Key", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Given_InvalidFileSize_When_CreateIsCalled_Then_ArgumentExceptionIsThrown(long fileSize)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Document.Create("file.pdf", "application/pdf", "key", fileSize, "user-1"));

        Assert.Equal("fileSize", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_InvalidUserId_When_CreateIsCalled_Then_ArgumentExceptionIsThrown(string? userId)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Document.Create("file.pdf", "application/pdf", "key", 100, userId!));

        Assert.Equal("userId", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_InvalidResume_When_SetResumeIsCalled_Then_ArgumentExceptionIsThrown(string? resume)
    {
        var document = Document.Create("file.pdf", "application/pdf", "key", 100, "user-1");

        var exception = Assert.Throws<ArgumentException>(() => document.SetResume(resume!));

        Assert.Equal("resume", exception.ParamName);
    }
}
