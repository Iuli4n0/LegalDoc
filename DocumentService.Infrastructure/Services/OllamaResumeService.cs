using System.Net;
using System.Text;
using DocumentService.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OllamaSharp;

namespace DocumentService.Infrastructure.Services;

public partial class OllamaResumeService : IResumeGeneratorService
{
    private const int DefaultChunkSize = 2500;
    private const int DefaultTimeoutSeconds = 300;
    private const int SingleResumeMaxWords = 400;
    private const int ChunkResumeMaxWords = 250;
    private const int CombinedResumeMaxWords = 800;
    private const double ChunkSplitThreshold = 0.15;
    private readonly OllamaApiClient _ollamaClient;
    private readonly string _model;
    private readonly int _chunkSize;
    private readonly ILogger<OllamaResumeService> _logger;

    public OllamaResumeService(IConfiguration configuration, ILogger<OllamaResumeService> logger)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        var endpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
        var configuredModel = configuration["Ollama:Model"];
        _model = string.IsNullOrWhiteSpace(configuredModel) ? "llama3.1:latest" : configuredModel.Trim();
        _chunkSize = int.TryParse(configuration["Ollama:ChunkSize"], out var cs) ? cs : DefaultChunkSize;

        _ollamaClient = new OllamaApiClient(new Uri(endpoint));

        LogInitialized(_logger, endpoint, _model, _chunkSize);
    }

    public async Task<ResumeResult> GenerateResumeAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty.", nameof(text));

        var chunks = SplitIntoChunks(text, _chunkSize);
        LogChunkSplit(_logger, chunks.Count, _chunkSize);

        if (chunks.Count == 1)
        {
            var resume = await GenerateSingleResumeAsync(chunks[0], cancellationToken).ConfigureAwait(false);
            return new ResumeResult(resume, 1);
        }

        var partialResumes = new List<string>();
        for (var i = 0; i < chunks.Count; i++)
        {
            LogChunkProcessing(_logger, i + 1, chunks.Count);
            var partialResume = await GenerateChunkResumeAsync(chunks[i], i + 1, chunks.Count, cancellationToken).ConfigureAwait(false);
            partialResumes.Add(partialResume);
        }

        LogCombiningResumes(_logger, partialResumes.Count);
        var finalResume = await CombineResumesAsync(partialResumes, cancellationToken).ConfigureAwait(false);

        return new ResumeResult(finalResume, chunks.Count);
    }

    private async Task<string> GenerateSingleResumeAsync(string text, CancellationToken cancellationToken)
    {
        var prompt = $"""
                      Acționezi ca un asistent juridic cu experiență. Sarcina ta este să generezi un rezumat clar, structurat și la obiect al următorului document juridic în limba română (maxim {SingleResumeMaxWords} cuvinte).

                      REGULI STRICTE PENTRU A EVITA HALUCINAȚIILE:
                      1. REZUMĂ EXCLUSIV TEXTUL FURNIZAT. Nu deduce, nu interpreta legea și nu adăuga detalii care nu sunt scrise negru pe alb în text.
                      2. Identifică și păstrează nealterate elementele cheie: părțile implicate, obiectul contractului/litigiului, temeiurile legale (dacă sunt menționate expres) și obligațiile/deciziile finale.
                      3. Dacă textul este ambiguu, menține ambiguitatea în rezumat; nu încerca să o rezolvi tu.

                      DOCUMENT JURIDIC:
                      {text}

                      REZUMATUL DOCUMENTULUI:
                      """;

        return await SendToOllamaAsync(prompt, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GenerateChunkResumeAsync(string chunkText, int chunkNumber, int totalChunks, CancellationToken cancellationToken)
    {
        var prompt = $"""
                      Acționezi ca un asistent juridic expert și obiectiv.
                      Aceasta este partea {chunkNumber} din {totalChunks} a unui document juridic.
                      Sarcina ta este să generezi un rezumat concis al acestui fragment în limba română (maxim {ChunkResumeMaxWords} cuvinte).

                      REGULI STRICTE PENTRU A EVITA HALUCINAȚIILE:
                      1. Bazează-te STRICT pe textul furnizat. NU inventa informații, nu face presupuneri și nu adăuga cunoștințe externe.
                      2. Păstrează exactitatea absolută a datelor: nume de persoane/instituții, date calendaristice, sume de bani și articole de lege menționate.
                      3. Nu interpreta textul și nu oferi sfaturi legale; doar rezumă faptele descrise.
                      4. Dacă fragmentul conține doar anteturi, semnături sau nu are substanță juridică, menționează scurt acest lucru.

                      TEXT FRAGMENT:
                      {chunkText}

                      REZUMATUL FRAGMENTULUI:
                      """;

        return await SendToOllamaAsync(prompt, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> CombineResumesAsync(List<string> partialResumes, CancellationToken cancellationToken)
    {
        var combined = string.Join("\n\n", partialResumes.Select((r, i) => $"Rezumat partea {i + 1}: {r}"));

        var prompt = $"""
                      Acționezi ca un asistent juridic expert. Mai jos se află o serie de rezumate parțiale (fragmente) extrase din același document juridic lung.
                      Sarcina ta este să le îmbini într-un singur rezumat final, coerent și logic în limba română (maxim {CombinedResumeMaxWords} cuvinte).

                      REGULI STRICTE PENTRU A EVITA HALUCINAȚIILE:
                      1. Folosește EXCLUSIV informațiile din rezumatele parțiale furnizate mai jos. Este strict interzis să adaugi detalii noi.
                      2. Elimină repetițiile dintre fragmente, dar asigură-te că păstrezi esența juridică: obiectul documentului, părțile implicate, argumentele principale și decizia/concluzia.
                      3. Păstrează un ton formal, neutru și obiectiv. Nu trage propriile tale concluzii.

                      REZUMATE PARȚIALE:
                      {combined}

                      REZUMATUL FINAL COERENT:
                      """;

        return await SendToOllamaAsync(prompt, cancellationToken).ConfigureAwait(false);
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
                $"Ollama model '{_model}' is not available. Pull it first (example: {pullCommand}).",
                ex);
        }
        catch (OperationCanceledException ex)
        {
            LogTimeout(_logger, ex, DefaultTimeoutSeconds);
            throw new TimeoutException($"Ollama request timed out after {DefaultTimeoutSeconds} seconds.");
        }
        catch (Exception ex)
        {
            LogFailure(_logger, ex);
            throw new InvalidOperationException($"Failed to communicate with Ollama: {ex.Message}", ex);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "OllamaResumeService initialized. Endpoint: {Endpoint}, Model: {Model}, ChunkSize: {ChunkSize}")]
    private static partial void LogInitialized(ILogger logger, string endpoint, string model, int chunkSize);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Text split into {ChunkCount} chunk(s) of max {ChunkSize} characters")]
    private static partial void LogChunkSplit(ILogger logger, int chunkCount, int chunkSize);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Processing chunk {Current}/{Total}")]
    private static partial void LogChunkProcessing(ILogger logger, int current, int total);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Combining {Count} partial resumes into final resume")]
    private static partial void LogCombiningResumes(ILogger logger, int count);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Ollama response received: {CharCount} characters")]
    private static partial void LogResponseReceived(ILogger logger, int charCount);

    [LoggerMessage(EventId = 6, Level = LogLevel.Error, Message = "Ollama returned 404. Model '{Model}' is likely missing; run `{PullCommand}`.")]
    private static partial void LogModelMissing(ILogger logger, Exception exception, string model, string pullCommand);

    [LoggerMessage(EventId = 7, Level = LogLevel.Error, Message = "Ollama request timed out after {TimeoutSeconds} seconds")]
    private static partial void LogTimeout(ILogger logger, Exception exception, int timeoutSeconds);

    [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "Failed to communicate with Ollama")]
    private static partial void LogFailure(ILogger logger, Exception exception);

    private static List<string> SplitIntoChunks(string text, int chunkSize)
    {
        var chunks = new List<string>();
        var currentIndex = 0;

        while (currentIndex < text.Length)
        {
            var remainingLength = text.Length - currentIndex;
            var length = Math.Min(chunkSize, remainingLength);

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
