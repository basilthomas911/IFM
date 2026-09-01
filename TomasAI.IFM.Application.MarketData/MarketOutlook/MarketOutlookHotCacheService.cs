using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento;

namespace TomasAI.IFM.Application.MarketData.MarketOutlook;

public interface IMarketDataGenerationAuthority
{
    bool TryGetActive(out MarketOutlookGenerationFence fence);
}

/// <summary>Current API-hosted adapter; the future Databento watchdog implements the same seam.</summary>
public sealed class DatabentoMarketDataGenerationAuthority(DatabentoMarketDataApi marketDataApi)
    : IMarketDataGenerationAuthority
{
    readonly object gate = new();
    bool running;
    string contractId = string.Empty;
    DateOnly valueDate;
    Guid generationId;

    public bool TryGetActive(out MarketOutlookGenerationFence fence)
    {
        var health = marketDataApi.GetHealth();
        if (!health.Running || health.ValueDate is not { } valueDate
            || !marketDataApi.TryGetCurrentlyTradedFuturesContract("ES", out var contract))
        {
            lock (gate)
                running = false;
            fence = default;
            return false;
        }
        lock (gate)
        {
            if (!running
                || !string.Equals(contractId, contract.ContractId, StringComparison.Ordinal)
                || this.valueDate != valueDate)
            {
                generationId = Guid.NewGuid();
                contractId = contract.ContractId;
                this.valueDate = valueDate;
            }
            running = true;
            fence = new MarketOutlookGenerationFence(contractId, this.valueDate, generationId);
        }
        return true;
    }
}

/// <summary>
/// Hosts the derived Market Outlook cache inside the current API process. It observes generation
/// authority but has no capability to mutate the Databento lifecycle.
/// </summary>
public sealed class MarketOutlookHotCacheService(
    IMarketOutlookHotCache cache,
    IMarketDataGenerationAuthority generationAuthority,
    ILogger<MarketOutlookHotCacheService> logger) : BackgroundService
{
    static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        MarketOutlookGenerationFence last = default;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (generationAuthority.TryGetActive(out var current) && current != last)
            {
                cache.Activate(current);
                last = current;
                logger.LogInformation(
                    "Market Outlook hot cache activated for {ContractId}/{ValueDate}.",
                    current.ContractId,
                    current.ValueDate);
            }
            // Do not cancel the short delay. Shutdown observes the token on the next tick without
            // manufacturing a routine TaskCanceledException in first-chance debugger output.
            await Task.Delay(PollInterval).ConfigureAwait(false);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        cache.Clear();
    }
}
