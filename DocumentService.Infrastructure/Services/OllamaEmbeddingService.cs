using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OllamaSharp;

namespace DocumentService.Infrastructure.Services;

public class OllamaEmbeddingService : IEmbeddingService
{
    private const int DefaultTimeoutSeconds = 120;

    private readonly OllamaApiClient _ollamaClient;
    private readonly string _model;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    public OllamaEmbeddingService(IConfiguration configuration, ILogger<OllamaEmbeddingService> logger)
    {
        _logger = logger;

        var endpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
        var configuredModel = configuration["Ollama:EmbeddingModel"];
        _model = string.IsNullOrWhiteSpace(configuredModel) ? "nomic-embed-text" : configuredModel.Trim();

        _ollamaClient = new OllamaApiClient(new Uri(endpoint));

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "OllamaEmbeddingService initialized. Endpoint: {Endpoint}, Model: {Model}",
                endpoint, _model);
        }
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty.", nameof(text));

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(DefaultTimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var request = new OllamaSharp.Models.EmbedRequest
            {
                Model = _model,
                Input = [text]
            };

            var response = await _ollamaClient.EmbedAsync(request, linkedCts.Token).ConfigureAwait(false);

            if (response?.Embeddings is null || response.Embeddings.Count == 0)
                throw new InvalidOperationException("Ollama returned no embeddings.");

            var embedding = response.Embeddings[0];

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Generated embedding with {Dimensions} dimensions", embedding.Length);
            }
            return embedding;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            var pullCommand = $"ollama pull {_model}";
            _logger.LogError(ex, "Ollama returned 404. Embedding model '{Model}' is likely missing; run `{PullCommand}`.", _model, pullCommand);
            throw new InvalidOperationException(
                $"Ollama embedding model '{_model}' is not available. Pull it first (example: {pullCommand}).",
                ex);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogError(ex, "Embedding request timed out after {TimeoutSeconds} seconds", DefaultTimeoutSeconds);
            throw new TimeoutException($"Embedding request timed out after {DefaultTimeoutSeconds} seconds.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException and not TimeoutException)
        {
            _logger.LogError(ex, "Failed to generate embedding with Ollama");
            throw new InvalidOperationException($"Failed to generate embedding: {ex.Message}", ex);
        }
    }
}
