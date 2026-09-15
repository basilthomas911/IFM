using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Event;

/// <summary>Handles one terminal historical acquisition completion.</summary>
public static class FuturesAnalyticsHistoricalDataLoaderCompleted
{
    /// <summary>Acknowledges the completion already recorded by the requested handler.</summary>
    public static ValueTask ExecuteAsync(
        this FuturesAnalyticsHistoricalDataLoaderCompletedEvent @event,
        IFuturesAnalyticsHistoricalDataLoaderEventContext context,
        ILogger<FuturesAnalyticsHistoricalDataLoaderEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        return ValueTask.CompletedTask;
    }
}
