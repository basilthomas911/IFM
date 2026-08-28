using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Event;

/// <summary>Handles delivery of the source Published event after its ACID event-log commit.</summary>
public static class FuturesTradeSessionBarPublished
{
    /// <summary>Accepts the source event; ScyllaDB work is owned exclusively by the Command EventProjector.</summary>
    public static ValueTask<bool> ExecuteAsync(
        this FuturesTradeSessionBarPublishedEvent @event,
        IFuturesTradeSessionBarSignalEventContext context,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        return ValueTask.FromResult(true);
    }
}
