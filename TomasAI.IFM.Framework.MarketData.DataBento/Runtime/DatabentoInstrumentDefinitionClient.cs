using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace TomasAI.IFM.Framework.MarketData.DataBento;

public interface IInstrumentDefinitionProvider
{
    IAsyncEnumerable<ExactInstrumentDefinition> ReadLatestAsync(string dataset, CancellationToken cancellationToken = default);
}

/// <summary>Streams the complete definition schema without price/timestamp rounding or field projection.</summary>
public sealed class DatabentoInstrumentDefinitionClient(HttpClient http, Func<string?>? getApiKey = null) : IInstrumentDefinitionProvider, IDisposable
{
    public void Dispose() => http.Dispose();
    static readonly Uri Endpoint = new("https://hist.databento.com/v0/");
    public async IAsyncEnumerable<ExactInstrumentDefinition> ReadLatestAsync(string dataset, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataset);
        var key = getApiKey is null ? Environment.GetEnvironmentVariable("DATABENTO_API_KEY") : getApiKey();
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("DATABENTO_API_KEY is required to refresh instrument definitions.");
        HttpRequestMessage Request(HttpMethod method, string path)
        {
            var request = new HttpRequestMessage(method, new Uri(Endpoint, path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(key + ":")));
            return request;
        }
        using var metadataRequest = Request(HttpMethod.Get, "metadata.get_dataset_range?dataset=" + Uri.EscapeDataString(dataset));
        using var metadataResponse = await http.SendAsync(metadataRequest, cancellationToken).ConfigureAwait(false);
        metadataResponse.EnsureSuccessStatusCode();
        using var metadata = JsonDocument.Parse(await metadataResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var end = metadata.RootElement.GetProperty("schema").GetProperty("definition").GetProperty("end").GetString()!;
        var start = DateOnly.ParseExact(end[..10], "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture).AddDays(-1).ToString("yyyy-MM-dd");
        using var request = Request(HttpMethod.Post, "timeseries.get_range");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["dataset"] = dataset, ["schema"] = "definition", ["symbols"] = "ALL_SYMBOLS", ["stype_in"] = "raw_symbol",
            ["start"] = start, ["end"] = end, ["encoding"] = "json", ["compression"] = "none",
            ["pretty_px"] = "false", ["pretty_ts"] = "false", ["map_symbols"] = "false"
        });
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line)) yield return ExactInstrumentDefinition.Parse(dataset, line);
        }
    }
}
