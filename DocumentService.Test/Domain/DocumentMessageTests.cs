using DocumentService.Domain.Entities;

namespace DocumentService.Test.Domain;

public class DocumentMessageTests
{
    [Fact]
    public void Given_ValidParameters_When_CreateIsCalled_Then_MessageIsCreatedCorrectly()
    {
        var documentId = Guid.NewGuid();
        var text = "What is this document about?";

        var message = DocumentMessage.Create(documentId, true, text);

        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal(documentId, message.DocumentId);
        Assert.True(message.IsUser);
        Assert.Equal(text, message.Text);
        Assert.Null(message.SourcesJson);
        Assert.True(message.CreatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Given_ValidParametersWithSources_When_CreateIsCalled_Then_SourcesJsonIsSet()
    {
        var documentId = Guid.NewGuid();
        var sourcesJson = "[{\"chunkIndex\":0}]";

        var message = DocumentMessage.Create(documentId, false, "Answer text.", sourcesJson);

        Assert.False(message.IsUser);
        Assert.Equal(sourcesJson, message.SourcesJson);
    }

    [Fact]
    public void Given_NullSourcesJson_When_CreateIsCalled_Then_SourcesJsonIsNull()
    {
        var message = DocumentMessage.Create(Guid.NewGuid(), true, "Question text.", null);
        Assert.Null(message.SourcesJson);
    }

    [Fact]
    public void Given_TwoMessagesCreated_When_Compared_Then_UniqueIdsAreGenerated()
    {
        var docId = Guid.NewGuid();
        var msg1 = DocumentMessage.Create(docId, true, "Q1");
        var msg2 = DocumentMessage.Create(docId, false, "A1");

        Assert.NotEqual(msg1.Id, msg2.Id);
    }

    [Fact]
    public void Given_EmptyDocumentId_When_CreateIsCalled_Then_ArgumentExceptionIsThrown()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            DocumentMessage.Create(Guid.Empty, true, "Text"));

        Assert.Equal("documentId", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_InvalidText_When_CreateIsCalled_Then_ArgumentExceptionIsThrown(string? text)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            DocumentMessage.Create(Guid.NewGuid(), true, text!));

        Assert.Equal("text", exception.ParamName);
    }
}
