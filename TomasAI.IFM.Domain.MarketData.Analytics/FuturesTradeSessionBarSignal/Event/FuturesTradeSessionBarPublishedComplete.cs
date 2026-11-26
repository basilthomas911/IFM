using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarSignal.Event;

/// <summary>Publishes the consumer realtime event only after successful ScyllaDB projection.</summary>
public static class FuturesTradeSessionBarPublishedComplete
{

    extension(FuturesTradeSessionBarPublishedCompleteEvent @event)
    {
        /// <summary>Forwards one persisted bar to all realtime bar-derived consumers.</summary>
        public async ValueTask<bool> ExecuteAsync(
            IFuturesTradeSessionBarSignalEventContext context,
            ILogger<FuturesTradeSessionBarSignalEventActor> logger)
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
                EventSource = nameof(FuturesTradeSessionBarSignalEventActor),
                ReceivedOn = @event.ReceivedOn,
                Observation = @event.Bar
            };
            var forwarded = MessageOrigins.Copy(@event, realtime);
            // Legacy persisted bodies may have no associated metadata. Keep unknown explicit rather than creating a retry-clock origin.
            MessageOrigins.Restore(forwarded, MessageOrigins.Get(@event));
            await context.SendAsync<FuturesTradeSessionBarClosedRealtimeEvent,
                FuturesTradeSessionBarEntityId>(forwarded).ConfigureAwait(false);
            FuturesTradeSessionBarPublicationProgress.Record(@event.Bar);
            return true;
        }
    }
}
