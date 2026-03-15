using System.Collections.Generic;
using DocumentService.Infrastructure.Services;
using Xunit;

namespace DocumentService.Test.Services;

public class OllamaClauseExtractionServiceTests
{
    [Fact]
    public void ParseClauses_WithValidDelimiters_ExtractsCorrectly()
    {
        var raw = """
                  <clause>Chiriaşul se obligă să plătească chiria lunară în cuantum de 500 EUR.</clause>
                  <clause>Contractul poate fi reziliat de oricare parte cu un preaviz de 30 de zile.</clause>
                  """;

        var clauses = OllamaClauseExtractionService.ParseClauses(raw);

        Assert.Equal(2, clauses.Count);
        Assert.Contains("Chiriaşul se obligă să plătească chiria lunară în cuantum de 500 EUR.", clauses);
        Assert.Contains("Contractul poate fi reziliat de oricare parte cu un preaviz de 30 de zile.", clauses);
    }

    [Fact]
    public void ParseClauses_WithNoDelimiters_ReturnsEmpty()
    {
        var raw = "Acesta este un text fara delimitatori de clauze.";
        var clauses = OllamaClauseExtractionService.ParseClauses(raw);
        Assert.Empty(clauses);
    }

    [Fact]
    public void ParseClauses_WithEmptyOrShortDelimiters_SkipsThem()
    {
        var raw = "<clause></clause><clause>Scurt</clause><clause>O clauză validă despre termeni contractuali care este suficient de lungă.</clause>";
        var clauses = OllamaClauseExtractionService.ParseClauses(raw);

        Assert.Single(clauses);
        Assert.Equal("O clauză validă despre termeni contractuali care este suficient de lungă.", clauses[0]);
    }

    [Fact]
    public void SplitIntoChunks_RespectsParagraphBoundaries()
    {
        var text = new string('A', 900) + "\n\n" + new string('B', 500);

        var chunks = OllamaClauseExtractionService.SplitIntoChunks(text, 1000);

        Assert.Equal(2, chunks.Count);
        Assert.StartsWith("B", chunks[1]);
        Assert.Equal(500, chunks[1].Length);
    }

    [Fact]
    public void SplitIntoChunks_SmallText_ReturnsSingleChunk()
    {
        var text = "Aceasta este o clauza scurta de test.";
        var chunks = OllamaClauseExtractionService.SplitIntoChunks(text, 1500);

        Assert.Single(chunks);
        Assert.Equal(text, chunks[0]);
    }
}
