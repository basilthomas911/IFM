using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarPublisher;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Realtime;

/// <summary>Forwards valid closed bars to the event-sourced EMA command actor.</summary>
public static class FuturesTradeSessionBarClosed
{
    /// <summary>Handles one routed bar without retaining realtime state.</summary>
    public static async ValueTask<bool> ExecuteAsync(this FuturesTradeSessionBarClosedRealtimeEvent @event,
        IFuturesEmaSignalRealtimeContext context, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        if (!@event.Observation.IsValid) return true;
        var result = await context.GenerateFuturesEmaSignalAsync(@event.Observation);
        if (result is ServiceFailed<GuidResult>)
            logger.LogError("EMA command rejected observation {ObservationId}.", @event.Observation.ObservationId);
        return result is not ServiceFailed<GuidResult>;
    }
}
