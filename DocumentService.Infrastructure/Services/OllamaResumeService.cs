using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OllamaSharp;

namespace DocumentService.Infrastructure.Services;

public class OllamaResumeService : IResumeGeneratorService
{
    private const int DefaultChunkSize = 1000;
    private const int DefaultTimeoutSeconds = 90;
    private const int SingleResumeMaxWords = 150;
    private const int ChunkResumeMaxWords = 100;
    private const int CombinedResumeMaxWords = 500;
    private const double ChunkSplitThreshold = 0.8;
    
    private readonly OllamaApiClient _ollamaClient;
    private readonly string _model;
    private readonly int _chunkSize;
    private readonly ILogger<OllamaResumeService> _logger;

    public OllamaResumeService(IConfiguration configuration, ILogger<OllamaResumeService> logger)
    {
        _logger = logger;

        var endpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
        _model = configuration["Ollama:Model"] ?? "llama3.1:latest";
        _chunkSize = int.TryParse(configuration["Ollama:ChunkSize"], out var cs) ? cs : DefaultChunkSize;

        _ollamaClient = new OllamaApiClient(new Uri(endpoint));

        _logger.LogInformation(
            "OllamaResumeService initialized. Endpoint: {Endpoint}, Model: {Model}, ChunkSize: {ChunkSize}",
            endpoint, _model, _chunkSize);
    }

    public async Task<ResumeResult> GenerateResumeAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty.", nameof(text));

        var chunks = SplitIntoChunks(text, _chunkSize);
        _logger.LogInformation("Text split into {ChunkCount} chunk(s) of max {ChunkSize} characters", chunks.Count, _chunkSize);

        if (chunks.Count == 1)
        {
            var resume = await GenerateSingleResumeAsync(chunks[0], cancellationToken);
            return new ResumeResult(resume, 1);
        }

        // Generează rezumate parțiale pentru fiecare chunk
        var partialResumes = new List<string>();
        for (var i = 0; i < chunks.Count; i++)
        {
            _logger.LogInformation("Processing chunk {Current}/{Total}", i + 1, chunks.Count);
            var partialResume = await GenerateChunkResumeAsync(chunks[i], i + 1, chunks.Count, cancellationToken);
            partialResumes.Add(partialResume);
        }

        // Combină rezumatele parțiale într-un rezumat final
        _logger.LogInformation("Combining {Count} partial resumes into final resume", partialResumes.Count);
        var finalResume = await CombineResumesAsync(partialResumes, cancellationToken);

        return new ResumeResult(finalResume, chunks.Count);
    }

    private async Task<string> GenerateSingleResumeAsync(string text, CancellationToken cancellationToken)
    {
        var prompt = $"""
                      Generează un rezumat concis în limba română al următorului document (maxim {SingleResumeMaxWords} cuvinte):

                      {text}
                      """;

        return await SendToOllamaAsync(prompt, cancellationToken);
    }

    private async Task<string> GenerateChunkResumeAsync(string chunkText, int chunkNumber, int totalChunks, CancellationToken cancellationToken)
    {
        var prompt = $"""
                      Aceasta este partea {chunkNumber} din {totalChunks} ale unui document.
                      Generează un rezumat concis în limba română al acestei părți (maxim {ChunkResumeMaxWords} cuvinte):

                      {chunkText}
                      """;

        return await SendToOllamaAsync(prompt, cancellationToken);
    }

    private async Task<string> CombineResumesAsync(List<string> partialResumes, CancellationToken cancellationToken)
    {
        var combined = string.Join("\n\n", partialResumes.Select((r, i) => $"Rezumat partea {i + 1}: {r}"));

        var prompt = $"""
                      Combină următoarele rezumate parțiale ale unui document într-un singur rezumat coerent în limba română (maxim {CombinedResumeMaxWords} cuvinte):

                      {combined}
                      """;

        return await SendToOllamaAsync(prompt, cancellationToken);
    }

    private async Task<string> SendToOllamaAsync(string prompt, CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(DefaultTimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var sb = new StringBuilder();

            await foreach (var stream in _ollamaClient.GenerateAsync(new OllamaSharp.Models.GenerateRequest
            {
                Model = _model,
                Prompt = prompt
            }, linkedCts.Token))
            {
                sb.Append(stream.Response);
            }

            var result = sb.ToString().Trim();
            _logger.LogInformation("Ollama response received: {CharCount} characters", result.Length);
            return result;
        }
        catch (OperationCanceledException exception)
        {
            _logger.LogError(exception, "Ollama request timed out after {TimeoutSeconds} seconds", DefaultTimeoutSeconds);
            throw new TimeoutException($"Ollama request timed out after {DefaultTimeoutSeconds} seconds.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to communicate with Ollama");
            throw new InvalidOperationException($"Failed to generate resume with Ollama: {ex.Message}", ex);
        }
    }

    private static List<string> SplitIntoChunks(string text, int chunkSize)
    {
        var chunks = new List<string>();
        var currentIndex = 0;

        while (currentIndex < text.Length)
        {
            var remainingLength = text.Length - currentIndex;
            var length = Math.Min(chunkSize, remainingLength);

            // Dacă am luat tot textul rămas, nu mai trebuie să căutăm punct de tăiere
            if (length >= remainingLength)
            {
                chunks.Add(text.Substring(currentIndex, length).Trim());
                break;
            }

            var chunk = text.Substring(currentIndex, length);
            var cutPoint = FindCutPoint(chunk, chunkSize);

            if (cutPoint > 0)
            {
                chunk = chunk[..cutPoint];
                length = cutPoint;
            }

            var trimmedChunk = chunk.Trim();
            if (trimmedChunk.Length > 0)
            {
                chunks.Add(trimmedChunk);
            }

            currentIndex += length;
        }

        return chunks;
    }

    private static int FindCutPoint(string chunk, int chunkSize)
    {
        var splitThreshold = chunkSize * ChunkSplitThreshold;
        var separators = new (int index, int offset)[]
        {
            (chunk.LastIndexOf("\n\n", StringComparison.Ordinal), 2),
            (chunk.LastIndexOf('\n'), 1),
            (chunk.LastIndexOf(' '), 0)
        };

        foreach (var (index, offset) in separators)
        {
            if (index > splitThreshold)
            {
                return index + offset;
            }
        }

        return -1;
    }
}
