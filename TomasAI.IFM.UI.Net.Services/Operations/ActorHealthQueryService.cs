using System.Net;
using System.Text.Json;
using TomasAI.IFM.UI.Net.Models.Operations;

namespace TomasAI.IFM.UI.Net.Services.Operations;

public interface IActorHealthQueryService
{
    Task<UiOperationResult<ActorHealthSnapshot>> GetAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);
}

public sealed class ActorHealthQueryService(
    HttpClient client,
    Uri? endpoint,
    bool ownsHttpClient = false) : IActorHealthQueryService, IDisposable
{
    const int MaximumResponseBytes = 8 * 1024 * 1024;
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { MaxDepth = 20 };

    public async Task<UiOperationResult<ActorHealthSnapshot>> GetAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        if (endpoint is null)
            return Failed("Actor health endpoint is not configured.");
        if (fromUtc > toUtc)
            return Failed("The From time must be before the To time.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var builder = new UriBuilder(endpoint);
            builder.Query = $"fromUtc={Uri.EscapeDataString(fromUtc.ToUniversalTime().ToString("O"))}&toUtc={Uri.EscapeDataString(toUtc.ToUniversalTime().ToString("O"))}";
            using var response = await client.GetAsync(builder.Uri, HttpCompletionOption.ResponseHeadersRead,
                deadline.Token).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK)
                return Failed($"Actor health is unavailable (HTTP {(int)response.StatusCode}).");
            if (response.Content.Headers.ContentLength > MaximumResponseBytes)
                return Failed("Actor health response exceeds its bounded size.");
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            var value = await JsonSerializer.DeserializeAsync<ActorHealthSnapshot>(stream, JsonOptions,
                deadline.Token).ConfigureAwait(false);
            return value is null || value.ObservedUtc == default || value.Actors.Count > 4096
                ? Failed("Actor health response is incomplete or outside its bounds.")
                : UiOperationResult<ActorHealthSnapshot>.Success(value);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed("Actor health query timed out.");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException)
        {
            return Failed("Actor health could not be read.");
        }
    }

    static UiOperationResult<ActorHealthSnapshot> Failed(string reason)
        => UiOperationResult<ActorHealthSnapshot>.Failure(9620, reason);

    public void Dispose()
    {
        if (ownsHttpClient)
            client.Dispose();
    }
}
