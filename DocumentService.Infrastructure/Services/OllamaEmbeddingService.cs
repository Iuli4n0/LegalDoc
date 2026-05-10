using System.Net;
using DocumentService.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OllamaSharp;

namespace DocumentService.Infrastructure.Services;

public partial class OllamaEmbeddingService : IEmbeddingService
{
    private const int DefaultTimeoutSeconds = 120;

    private readonly OllamaApiClient _ollamaClient;
    private readonly string _model;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    public OllamaEmbeddingService(IConfiguration configuration, ILogger<OllamaEmbeddingService> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        var endpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
        var configuredModel = configuration["Ollama:EmbeddingModel"];
        _model = string.IsNullOrWhiteSpace(configuredModel) ? "nomic-embed-text" : configuredModel.Trim();

        _ollamaClient = new OllamaApiClient(new Uri(endpoint));

        LogInitialized(_logger, endpoint, _model);
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

            if (response.Embeddings.Count == 0)
                throw new InvalidOperationException("Ollama returned no embeddings.");

            var embedding = response.Embeddings[0];
            LogEmbeddingGenerated(_logger, embedding.Length);
            return embedding;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            var pullCommand = $"ollama pull {_model}";
            LogModelMissing(_logger, ex, _model, pullCommand);
            throw new InvalidOperationException(
                $"Ollama embedding model '{_model}' is not available. Pull it first (example: {pullCommand}).",
                ex);
        }
        catch (OperationCanceledException ex)
        {
            LogTimeout(_logger, ex, DefaultTimeoutSeconds);
            throw new TimeoutException($"Embedding request timed out after {DefaultTimeoutSeconds} seconds.");
        }
        catch (Exception ex) when (ex is not InvalidOperationException and not TimeoutException)
        {
            LogFailure(_logger, ex);
            throw new InvalidOperationException($"Failed to generate embedding: {ex.Message}", ex);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "OllamaEmbeddingService initialized. Endpoint: {Endpoint}, Model: {Model}")]
    private static partial void LogInitialized(ILogger logger, string endpoint, string model);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "Generated embedding with {Dimensions} dimensions")]
    private static partial void LogEmbeddingGenerated(ILogger logger, int dimensions);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Ollama returned 404. Embedding model '{Model}' is likely missing; run `{PullCommand}`.")]
    private static partial void LogModelMissing(ILogger logger, Exception exception, string model, string pullCommand);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Embedding request timed out after {TimeoutSeconds} seconds")]
    private static partial void LogTimeout(ILogger logger, Exception exception, int timeoutSeconds);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Failed to generate embedding with Ollama")]
    private static partial void LogFailure(ILogger logger, Exception exception);
}
