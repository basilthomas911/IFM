using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Event;

/// <summary>Publishes the consumer realtime event only after successful ScyllaDB projection.</summary>
public static class FuturesTradeSessionBarPublishedComplete
{
    /// <summary>Forwards one persisted bar to all realtime bar-derived consumers.</summary>
    public static async ValueTask<bool> ExecuteAsync(
        this FuturesTradeSessionBarPublishedCompleteEvent @event,
        IFuturesTradeSessionBarPublisherEventContext context,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        var realtime = new FuturesTradeSessionBarClosedRealtimeEvent
        {
            Subject = new(
                ActorType.Realtime,
                FuturesTradeSessionBarClosedRealtimeEvent.Actor,
                FuturesTradeSessionBarClosedRealtimeEvent.Verb,
                @event.EntityId.Format()),
            Id = @event.Bar.ObservationId.Value,
            EntityId = @event.EntityId,
            CommandId = @event.CommandId,
            AggregateId = @event.EntityId.Format(),
            EventSource = nameof(FuturesTradeSessionBarPublisherEventActor),
            ReceivedOn = context.TimeProvider.GetUtcNow().UtcDateTime,
            Observation = @event.Bar
        };
        await context.SendAsync<FuturesTradeSessionBarClosedRealtimeEvent,
            FuturesTradeSessionBarEntityId>(realtime).ConfigureAwait(false);
        return true;
    }
}
