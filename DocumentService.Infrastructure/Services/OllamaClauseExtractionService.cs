using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OllamaSharp;

namespace DocumentService.Infrastructure.Services;

public class OllamaClauseExtractionService : IClauseExtractorService
{
    private const int DefaultChunkSize = 2000;
    private const int DefaultTimeoutSeconds = 120;
    private const string ClauseStartDelimiter = "<clause>";
    private const string ClauseEndDelimiter = "</clause>";

    private readonly OllamaApiClient _ollamaClient;
    private readonly string _model;
    private readonly int _chunkSize;
    private readonly ILogger<OllamaClauseExtractionService> _logger;

    public OllamaClauseExtractionService(IConfiguration configuration, ILogger<OllamaClauseExtractionService> logger)
    {
        _logger = logger;

        var endpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
        _model = configuration["Ollama:Model"] ?? "llama3.1:latest";
        _chunkSize = int.TryParse(configuration["Ollama:ClauseChunkSize"], out var cs) ? cs : DefaultChunkSize;

        _ollamaClient = new OllamaApiClient(new Uri(endpoint));

        _logger.LogInformation("OllamaClauseExtractionService initialized. Endpoint: {Endpoint}, Model: {Model}", endpoint, _model);
    }

    public async Task<ClauseExtractionResult> ExtractClausesAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty.", nameof(text));

        var chunks = SplitIntoChunks(text, _chunkSize);
        _logger.LogInformation("Text split into {ChunkCount} chunk(s).", chunks.Count);

        var allClauses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < chunks.Count; i++)
        {
            _logger.LogInformation("Processing chunk {Current}/{Total}", i + 1, chunks.Count);
            var rawResponse = await ExtractChunkClausesAsync(chunks[i], cancellationToken);
            var parsedClauses = ParseClauses(rawResponse);
            
            foreach (var clause in parsedClauses)
            {
                allClauses.Add(clause);
            }
        }

        return new ClauseExtractionResult(allClauses.ToList(), chunks.Count);
    }

    private async Task<string> ExtractChunkClausesAsync(string chunkText, CancellationToken cancellationToken)
    {
        var prompt = $"""
                      Ești un asistent juridic expert. Sarcina ta este să extragi toate clauzele contractuale/juridice din textul furnizat.
                      
                      REGULI SIMPLE ȘI STRICTE:
                      1. Extrage DOAR textul exact al clauzelor. Nu adăuga absolut niciun cuvânt în plus.
                      2. Pune FIX fiecare clauză extrasă între tag-urile {ClauseStartDelimiter} și {ClauseEndDelimiter}.
                      3. Dacă textul nu conține nicio clauză relevantă, nu răspunde cu nimic (lasă gol).

                      EXEMPLU DE RĂSPUNS AȘTEPTAT:
                      {ClauseStartDelimiter}Prezentul contract intră în vigoare la data semnării.{ClauseEndDelimiter}
                      {ClauseStartDelimiter}Părțile se obligă să păstreze confidențialitatea informațiilor.{ClauseEndDelimiter}

                      TEXT DE ANALIZAT:
                      {chunkText}
                      """;

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(DefaultTimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var sb = new StringBuilder();
            var request = new OllamaSharp.Models.GenerateRequest
            {
                Model = _model,
                Prompt = prompt
            };

            await foreach (var stream in _ollamaClient.GenerateAsync(request, linkedCts.Token))
            {
                if (stream != null) sb.Append(stream.Response);
            }

            return sb.ToString();
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("Ollama request timed out after {TimeoutSeconds} seconds", DefaultTimeoutSeconds);
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract clauses from Ollama.");
            return string.Empty;
        }
    }

    internal static List<string> ParseClauses(string rawResponse)
    {
        var clauses = new List<string>();
        if (string.IsNullOrWhiteSpace(rawResponse))
            return clauses;

        var searchFrom = 0;
        while (true)
        {
            var startIdx = rawResponse.IndexOf(ClauseStartDelimiter, searchFrom, StringComparison.OrdinalIgnoreCase);
            if (startIdx < 0) break;

            var contentStart = startIdx + ClauseStartDelimiter.Length;
            var endIdx = rawResponse.IndexOf(ClauseEndDelimiter, contentStart, StringComparison.OrdinalIgnoreCase);
            if (endIdx < 0) break;

            var clause = rawResponse.Substring(contentStart, endIdx - contentStart).Trim();
            if (!string.IsNullOrWhiteSpace(clause) && clause.Length > 10)
            {
                clauses.Add(clause);
            }

            searchFrom = endIdx + ClauseEndDelimiter.Length;
        }

        return clauses;
    }

    internal static List<string> SplitIntoChunks(string text, int chunkSize)
    {
        var chunks = new List<string>();
        var span = text.AsSpan();

        while (span.Length > 0)
        {
            if (span.Length <= chunkSize)
            {
                chunks.Add(span.ToString().Trim());
                break;
            }

            var chunkSpan = span.Slice(0, chunkSize);
            var splitIndex = chunkSpan.LastIndexOf("\n\n");
            if (splitIndex == -1) splitIndex = chunkSpan.LastIndexOf('\n');
            if (splitIndex == -1) splitIndex = chunkSpan.LastIndexOf(' ');
            
            if (splitIndex == -1) splitIndex = chunkSize;

            chunks.Add(span.Slice(0, splitIndex).ToString().Trim());
            span = span.Slice(splitIndex).TrimStart();
        }

        return chunks;
    }
}
