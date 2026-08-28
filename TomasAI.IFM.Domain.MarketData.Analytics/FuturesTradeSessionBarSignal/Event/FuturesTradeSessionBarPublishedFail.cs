using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Event;

/// <summary>Handles terminal ScyllaDB projection failures for published trade-session bars.</summary>
public static class FuturesTradeSessionBarPublishedFail
{
    /// <summary>Logs the terminal projection failure without publishing an unpersisted realtime bar.</summary>
    public static ValueTask<bool> ExecuteAsync(
        this FuturesTradeSessionBarPublishedFailEvent @event,
        IFuturesTradeSessionBarSignalEventContext context,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        logger.LogError(
            "Trade-session bar projection failed for {EntityId}: {ErrorMessage}",
            @event.EntityId,
            @event.ErrorMessage);
        return ValueTask.FromResult(false);
    }
}
