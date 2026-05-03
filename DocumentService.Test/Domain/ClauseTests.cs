using DocumentService.Domain.Entities;

namespace DocumentService.Test.Domain;

public class ClauseTests
{
    [Fact]
    public void Given_ValidParameters_When_CreateIsCalled_Then_ClauseIsCreatedCorrectly()
    {
        var documentId = Guid.NewGuid();
        var text = "This is a valid clause text.";

        var clause = Clause.Create(documentId, text);

        Assert.NotEqual(Guid.Empty, clause.Id);
        Assert.Equal(documentId, clause.DocumentId);
        Assert.Equal(text, clause.Text);
        Assert.True(clause.ExtractedAt <= DateTime.UtcNow);
        Assert.Null(clause.IsAbusive);
        Assert.Null(clause.AbusiveProbability);
        Assert.Null(clause.ClassifiedAt);
    }

    [Fact]
    public void Given_TwoClausesCreated_When_Compared_Then_UniqueIdsAreGenerated()
    {
        var docId = Guid.NewGuid();
        var clause1 = Clause.Create(docId, "Clause one text.");
        var clause2 = Clause.Create(docId, "Clause two text.");

        Assert.NotEqual(clause1.Id, clause2.Id);
    }

    [Fact]
    public void Given_EmptyDocumentId_When_CreateIsCalled_Then_ArgumentExceptionIsThrown()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Clause.Create(Guid.Empty, "Some clause text."));

        Assert.Equal("documentId", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Given_InvalidText_When_CreateIsCalled_Then_ArgumentExceptionIsThrown(string? text)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Clause.Create(Guid.NewGuid(), text!));

        Assert.Equal("text", exception.ParamName);
    }

    [Fact]
    public void Given_ValidClassification_When_SetClassificationIsCalled_Then_PropertiesAreSet()
    {
        var clause = Clause.Create(Guid.NewGuid(), "A valid clause.");

        clause.SetClassification(true, 0.85);

        Assert.True(clause.IsAbusive);
        Assert.Equal(0.85, clause.AbusiveProbability);
        Assert.NotNull(clause.ClassifiedAt);
        Assert.True(clause.ClassifiedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void Given_NonAbusiveClassification_When_SetClassificationIsCalled_Then_IsAbusiveIsFalse()
    {
        var clause = Clause.Create(Guid.NewGuid(), "A valid clause.");

        clause.SetClassification(false, 0.1);

        Assert.False(clause.IsAbusive);
        Assert.Equal(0.1, clause.AbusiveProbability);
    }

    [Fact]
    public void Given_BoundaryProbabilityZero_When_SetClassificationIsCalled_Then_Succeeds()
    {
        var clause = Clause.Create(Guid.NewGuid(), "A clause.");
        clause.SetClassification(false, 0.0);
        Assert.Equal(0.0, clause.AbusiveProbability);
    }

    [Fact]
    public void Given_BoundaryProbabilityOne_When_SetClassificationIsCalled_Then_Succeeds()
    {
        var clause = Clause.Create(Guid.NewGuid(), "A clause.");
        clause.SetClassification(true, 1.0);
        Assert.Equal(1.0, clause.AbusiveProbability);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(-1.0)]
    [InlineData(2.0)]
    public void Given_InvalidProbability_When_SetClassificationIsCalled_Then_ArgumentOutOfRangeExceptionIsThrown(double probability)
    {
        var clause = Clause.Create(Guid.NewGuid(), "A clause.");

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            clause.SetClassification(true, probability));

        Assert.Equal("abusiveProbability", exception.ParamName);
    }
}
