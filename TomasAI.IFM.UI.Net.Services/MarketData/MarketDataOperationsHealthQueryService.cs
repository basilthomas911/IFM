using System.Net;
using System.Text.Json;
using TomasAI.IFM.UI.Net.Models.MarketData;
using TomasAI.IFM.UI.Net.Services.Operations;

namespace TomasAI.IFM.UI.Net.Services.MarketData;

/// <summary>Independent, read-only operations-health query, outside the market-event delivery path.</summary>
public interface IMarketDataOperationsHealthQueryService
{
    Task<UiOperationResult<MarketDataOperationsHealthSnapshot>> GetAsync(CancellationToken cancellationToken = default);
}

/// <summary>Uses the configured central HTTP health endpoint; never invokes a recovery command.</summary>
public sealed class MarketDataOperationsHealthQueryService : IMarketDataOperationsHealthQueryService, IDisposable
{
    const int MaximumResponseBytes = 1024 * 1024;
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { MaxDepth = 16 };
    readonly HttpClient client;
    readonly Uri? endpoint;
    readonly bool ownsHttpClient;
    readonly TimeProvider time;

    public MarketDataOperationsHealthQueryService(HttpClient client, Uri? endpoint, bool ownsHttpClient = false,
        TimeProvider? timeProvider = null)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        if (endpoint is not null && (!endpoint.IsAbsoluteUri || endpoint.Scheme is not ("http" or "https")))
            throw new ArgumentException("An absolute HTTP(S) operations-health endpoint is required.", nameof(endpoint));
        this.endpoint = endpoint;
        this.ownsHttpClient = ownsHttpClient;
        time = timeProvider ?? TimeProvider.System;
    }

    public async Task<UiOperationResult<MarketDataOperationsHealthSnapshot>> GetAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (endpoint is null)
            return Failed("Operations health endpoint is not configured for this environment.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            using var response = await client.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead,
                deadline.Token).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
                return Failed($"Operations health is unavailable (HTTP {(int)response.StatusCode}).");
            if (response.Content.Headers.ContentLength > MaximumResponseBytes)
                return Failed("Operations health response exceeds its bounded size.");
            await using var input = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            using var payload = new MemoryStream();
            var buffer = new byte[8192];
            while (true)
            {
                var count = await input.ReadAsync(buffer, deadline.Token).ConfigureAwait(false);
                if (count == 0) break;
                if (payload.Length + count > MaximumResponseBytes)
                    return Failed("Operations health response exceeds its bounded size.");
                payload.Write(buffer, 0, count);
            }
            payload.Position = 0;
            var value = await JsonSerializer.DeserializeAsync<MarketDataOperationsHealthSnapshot>(
                payload, JsonOptions, deadline.Token).ConfigureAwait(false);
            if (value is null || value.SchemaVersion != 1 || value.ObservedOnUtc == default
                || value.Stages is null || value.Datasets is null
                || value.Stages.Count > 64 || value.Datasets.Count > 16 || !ValidStatus(value.OverallStatus)
                || value.Stages.Any(stage => stage is null || !ValidStatus(stage.Status)
                    || !Bounded(stage.Stage, 128) || !Bounded(stage.ReasonCode, 128) || !Bounded(stage.Reason, 4096))
                || value.Datasets.Any(dataset => dataset is null || !ValidStatus(dataset.Status)
                    || !Bounded(dataset.Dataset, 64) || !Bounded(dataset.Reason, 4096)))
                return Failed("Operations health response is incomplete or outside its bounds.");
            var observationAge = time.GetUtcNow().UtcDateTime - value.ObservedOnUtc.ToUniversalTime();
            if (observationAge > TimeSpan.FromSeconds(15) || observationAge < TimeSpan.FromSeconds(-30))
                return Failed("Central operations observation is stale or its clock is invalid; current health is unknown.");
            return UiOperationResult<MarketDataOperationsHealthSnapshot>.Success(value);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed("Operations health query timed out; current health is unknown.");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException)
        {
            return Failed("Operations health could not be read; current health is unknown.");
        }
    }

    static UiOperationResult<MarketDataOperationsHealthSnapshot> Failed(string reason) =>
        UiOperationResult<MarketDataOperationsHealthSnapshot>.Failure(9610, reason);

    static bool Bounded(string? value, int maximum) => value is not null && value.Length <= maximum;
    static bool ValidStatus(string? value) => value is "Inactive" or "Green" or "Yellow" or "Orange" or "Red";

    public void Dispose()
    {
        if (ownsHttpClient) client.Dispose();
    }
}
