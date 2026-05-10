using System.Text;
using DocumentService.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OllamaSharp;

namespace DocumentService.Infrastructure.Services;

public partial class OllamaClauseExtractionService : IClauseExtractorService
{
    private const int DefaultChunkSize = 2000;
    private const int DefaultTimeoutSeconds = 600;
    private const string ClauseStartDelimiter = "<clause>";
    private const string ClauseEndDelimiter = "</clause>";

    private readonly OllamaApiClient _ollamaClient;
    private readonly string _model;
    private readonly int _chunkSize;
    private readonly ILogger<OllamaClauseExtractionService> _logger;

    public OllamaClauseExtractionService(HttpClient httpClient, IConfiguration configuration, ILogger<OllamaClauseExtractionService> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;

        var endpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
        _model = configuration["Ollama:Model"] ?? "llama3.1:latest";
        _chunkSize = int.TryParse(configuration["Ollama:ClauseChunkSize"], out var cs) ? cs : DefaultChunkSize;

        if (httpClient.BaseAddress == null)
        {
            httpClient.BaseAddress = new Uri(endpoint);
        }

        _ollamaClient = new OllamaApiClient(httpClient);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            LogInitialized(_logger, endpoint, _model);
        }
    }

    public async Task<ClauseExtractionResult> ExtractClausesAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty.", nameof(text));

        var chunks = SplitIntoChunks(text, _chunkSize);
        if (_logger.IsEnabled(LogLevel.Information))
        {
            LogChunkSplit(_logger, chunks.Count);
        }

        var allClauses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < chunks.Count; i++)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                LogChunkProcessing(_logger, i + 1, chunks.Count);
            }

            var rawResponse = await ExtractChunkClausesAsync(chunks[i], cancellationToken).ConfigureAwait(false);
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
                      Ești un asistent juridic expert cu atenție maximă la detalii. Sarcina ta este să extragi exact (copy-paste) toate clauzele contractuale/juridice din textul furnizat.

                      DEFINIȚIA CLAUZEI:
                      O clauză reprezintă o obligație, un drept, o condiție, o penalitate sau o prevedere legală completă.
                      ATENȚIE: O clauză poate fi o singură propoziție, DAR foarte des este un paragraf întreg, format din mai multe propoziții interconectate, sau chiar o enumerare. Extrage ideea juridică în întregimea ei.

                      REGULI STRICTE DE EXTRAGERE:
                      1. EXTRAGERE VERBATIM: Copiază textul exact cum apare în sursă. Nu rezuma, nu modifica și nu omite niciun cuvânt din interiorul clauzei.
                      2. GRUPARE CORECTĂ: Dacă mai multe propoziții formează o singură regulă/clauză logică, include-le pe TOATE între aceleași tag-uri. Nu le sparge artificial.
                      3. DELIMITARE EXACTĂ: Pune FIX fiecare clauză completă între tag-urile {ClauseStartDelimiter} și {ClauseEndDelimiter}.
                      4. FĂRĂ CONVERSAȚIE: Nu adăuga absolut niciun cuvânt de politețe sau explicație (fără "Iată clauzele:", fără "Rezultat:"). Doar tag-urile și textul.
                      5. TEXT FĂRĂ CLAUZE: Dacă textul nu conține prevederi juridice clare, nu returna absolut nimic (lasă răspunsul complet gol).

                      EXEMPLU DE RĂSPUNS AȘTEPTAT (observă cum clauza a doua conține mai multe propoziții care formează un tot unitar):
                      {ClauseStartDelimiter}Prezentul contract intră în vigoare la data semnării.{ClauseEndDelimiter}
                      {ClauseStartDelimiter}Părțile se obligă să păstreze confidențialitatea informațiilor. Această obligație este valabilă pe o perioadă de 5 ani de la încetarea contractului. Orice încălcare a acestei prevederi atrage după sine plata unor daune-interese în valoare de 10.000 EUR.{ClauseEndDelimiter}

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

            await foreach (var stream in _ollamaClient.GenerateAsync(request, linkedCts.Token).ConfigureAwait(false))
            {
                if (stream != null) sb.Append(stream.Response);
            }

            return sb.ToString();
        }
        catch (OperationCanceledException ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                LogTimeout(_logger, ex, DefaultTimeoutSeconds);
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                LogFailure(_logger, ex);
            }

            return string.Empty;
        }
    }

    public static List<string> ParseClauses(string rawResponse)
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

    public static List<string> SplitIntoChunks(string text, int chunkSize)
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

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "OllamaClauseExtractionService initialized. Endpoint: {Endpoint}, Model: {Model}")]
    private static partial void LogInitialized(ILogger logger, string endpoint, string model);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Text split into {ChunkCount} chunk(s).")]
    private static partial void LogChunkSplit(ILogger logger, int chunkCount);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Processing chunk {Current}/{Total}")]
    private static partial void LogChunkProcessing(ILogger logger, int current, int total);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Ollama request timed out after {TimeoutSeconds} seconds")]
    private static partial void LogTimeout(ILogger logger, Exception exception, int timeoutSeconds);

    [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Failed to extract clauses from Ollama.")]
    private static partial void LogFailure(ILogger logger, Exception exception);
}
