using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DocumentService.Application.Abstractions;

namespace DocumentService.Infrastructure.Services;

public class ClauseClassificationService : IClauseClassificationService
{
    private readonly HttpClient _httpClient;

    public ClauseClassificationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ClauseClassificationResult> ClassifyAsync(string clauseText, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/predict", new PredictRequest(clauseText), cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<PredictResponse>(cancellationToken: cancellationToken);
        if (payload is null)
        {
            throw new InvalidOperationException("Classifier returned an empty response.");
        }

        return new ClauseClassificationResult(payload.Label, payload.ProbabilitateAbuziv);
    }

    private sealed record PredictRequest([property: JsonPropertyName("clauza")] string Clauza);

    private sealed record PredictResponse(
        [property: JsonPropertyName("label")] int Label,
        [property: JsonPropertyName("probabilitate_abuziv")] double ProbabilitateAbuziv);
}
