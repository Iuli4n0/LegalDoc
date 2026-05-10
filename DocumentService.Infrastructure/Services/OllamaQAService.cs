using System.Net;
using System.Text;
using DocumentService.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OllamaSharp;

namespace DocumentService.Infrastructure.Services;

public partial class OllamaQAService : IQAService
{
    private const int DefaultTimeoutSeconds = 300;
    private const int MaxAnswerWords = 600;

    private readonly OllamaApiClient _ollamaClient;
    private readonly string _model;
    private readonly ILogger<OllamaQAService> _logger;

    public OllamaQAService(IConfiguration configuration, ILogger<OllamaQAService> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        var endpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
        var configuredModel = configuration["Ollama:Model"];
        _model = string.IsNullOrWhiteSpace(configuredModel) ? "llama3.1:latest" : configuredModel.Trim();

        _ollamaClient = new OllamaApiClient(new Uri(endpoint));

        LogInitialized(_logger, endpoint, _model);
    }

    public async Task<string> GenerateAnswerAsync(string question, string[] contextChunks, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Question cannot be empty.", nameof(question));

        if (contextChunks is null || contextChunks.Length == 0)
            throw new ArgumentException("Context chunks cannot be empty.", nameof(contextChunks));

        var contextText = BuildContextText(contextChunks);

        var prompt = $"""
                      Ești un asistent juridic și de analiză a documentelor de înaltă expertiză. Răspunde la întrebarea utilizatorului STRICT pe baza fragmentelor de document furnizate mai jos.
                      
                      REGULI ESENȚIALE:
                      1. Folosește EXCLUSIV informațiile care se regăsesc în mod evident în fragmentele de mai jos. Fii cu un nivel extrem de atenție la detalii. Nu inventa clauze, nu completa cu legislație sau cunoștințe externe și nu presupune fapte!
                      2. Dacă un răspuns nu se află în textul oferit sau ești ambiguu, trebuie neapărat să comunici asta simplu: "Conform informațiilor primite, nu pot răspunde exact, deoarece detaliul nu este stipulat în mod clar în secțiunile de interes găsite."
                      3. Răspunde natural în limba română curată (diacritice corecte), cu fraze precise și o coeziune bună. 
                      4. Citează! Când formulezi răspunsul, dacă există reguli/procente/articole specifice, folosește Citate Directe punându-le între ghilimele ca să susții răspunsul. E foarte important să fii fact-based.
                      5. Nu repeta întrebarea la început. Structurează răspunsul folosind liste (`-`) dacă are sens pentru lizibilitate. Maximum {MaxAnswerWords} cuvinte.

                      FRAGMENTE EXTRASE DIN DOCUMENT:
                      ---
                      {contextText}
                      ---

                      ÎNTREBAREA UTILIZATORULUI:
                      {question}

                      RĂSPUNS ORGANIZAT:
                      """;

        return await SendToOllamaAsync(prompt, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildContextText(string[] chunks)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < chunks.Length; i++)
        {
            sb.AppendLine($"--- Fragment {i + 1} ---");
            sb.AppendLine(chunks[i]);
            sb.AppendLine();
        }
        return sb.ToString();
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
            }, linkedCts.Token).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(stream?.Response))
                {
                    sb.Append(stream.Response);
                }
            }

            var result = sb.ToString().Trim();
            LogResponseReceived(_logger, result.Length);
            return result;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            var pullCommand = $"ollama pull {_model}";
            LogModelMissing(_logger, ex, _model, pullCommand);
            throw new InvalidOperationException(
                $"Ollama model '{_model}' is not available. Pull it first.", ex);
        }
        catch (OperationCanceledException ex)
        {
            LogTimeout(_logger, ex, DefaultTimeoutSeconds);
            throw new TimeoutException($"Q&A request timed out after {DefaultTimeoutSeconds} seconds.");
        }
        catch (Exception ex)
        {
            LogFailure(_logger, ex);
            throw new InvalidOperationException($"Failed to generate answer: {ex.Message}", ex);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "OllamaQAService initialized. Endpoint: {Endpoint}, Model: {Model}")]
    private static partial void LogInitialized(ILogger logger, string endpoint, string model);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Q&A response received: {CharCount} characters")]
    private static partial void LogResponseReceived(ILogger logger, int charCount);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Ollama returned 404. Model '{Model}' is likely missing; run `{PullCommand}`.")]
    private static partial void LogModelMissing(ILogger logger, Exception exception, string model, string pullCommand);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Q&A request timed out after {TimeoutSeconds} seconds")]
    private static partial void LogTimeout(ILogger logger, Exception exception, int timeoutSeconds);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Failed to generate Q&A answer with Ollama")]
    private static partial void LogFailure(ILogger logger, Exception exception);
}
