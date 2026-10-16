using System.Net.Http.Json;
using TomasAI.IFM.Domain.SystemAdmin.Shared.MarketClose;

namespace TomasAI.IFM.Application.ScheduledTask.Shared;

/// <summary>Sends the parameterless close request to the running API and awaits its business outcome.</summary>
public sealed class MarketCloseClient(HttpClient client) : IMarketCloseApi
{
    public async Task<MarketCloseResult> CloseMarketAsync()
    {
        using var response = await client.PostAsync("api/market-data/close", null).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Market Close failed ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync().ConfigureAwait(false)}");
        return await response.Content.ReadFromJsonAsync<MarketCloseResult>().ConfigureAwait(false)
            ?? throw new InvalidOperationException("Market Close returned no completion result.");
    }
}
