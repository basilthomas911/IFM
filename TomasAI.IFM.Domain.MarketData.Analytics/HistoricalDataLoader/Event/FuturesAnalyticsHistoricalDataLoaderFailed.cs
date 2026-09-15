using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Event;

/// <summary>Handles one terminal historical acquisition failure.</summary>
public static class FuturesAnalyticsHistoricalDataLoaderFailed
{
    /// <summary>Acknowledges the typed failure already published by the requested handler.</summary>
    public static ValueTask ExecuteAsync(
        this FuturesAnalyticsHistoricalDataLoaderFailedEvent @event,
        IFuturesAnalyticsHistoricalDataLoaderEventContext context,
        ILogger<FuturesAnalyticsHistoricalDataLoaderEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        return ValueTask.CompletedTask;
    }
}
