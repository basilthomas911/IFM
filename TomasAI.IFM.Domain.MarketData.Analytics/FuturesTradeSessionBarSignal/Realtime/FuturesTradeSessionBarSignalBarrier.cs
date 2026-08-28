using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Realtime;

/// <summary>Handles the private server-clock barrier used to close elapsed trade-session bars.</summary>
public static class FuturesTradeSessionBarSignalBarrier
{
    /// <summary>Forwards every bar completed by the UTC barrier to the Command actor.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesTradeSessionBarSignalBarrierRealtimeEvent @event,
        IFuturesTradeSessionBarSignalRealtimeContext context,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        foreach (var bar in context.Accumulator.CloseThrough(@event.BarrierUtc))
            _ = await context.PublishFuturesTradeSessionBarAsync(bar).ConfigureAwait(false);
        return true;
    }
}
