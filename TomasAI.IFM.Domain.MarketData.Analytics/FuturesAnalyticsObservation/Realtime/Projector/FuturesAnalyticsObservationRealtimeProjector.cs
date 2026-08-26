using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAnalyticsObservation.Realtime.Projector;

/// <summary>Persists each immutable observation once before publishing it to bar-derived consumers.</summary>
public sealed class FuturesAnalyticsObservationRealtimeProjector(
    IHistoricalObservationStore store,
    IMarketSessionCalendar calendar,
    TimeProvider timeProvider)
{
    /// <summary>Persists and publishes a newly closed observation.</summary>
    public async ValueTask<bool> ProjectAsync(
        IEventActorContext context,
        FuturesAnalyticsObservationReadModel observation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(observation);
        if (!await store.TryWriteObservationAsync(observation, cancellationToken).ConfigureAwait(false))
            return false;
        if (observation.TimeFrame == TimeFrameType.Daily)
        {
            var session = calendar.GetSession(observation.ValueDate);
            await store.TryWriteRawEodAsync(new FuturesEodObservationReadModel
            {
                MarketSeriesIdentity = observation.MarketSeriesIdentity,
                ContractId = observation.ContractId,
                ValueDate = observation.ValueDate,
                SessionStartUtc = session.StartUtc,
                SessionEndUtc = session.EndUtc,
                Open = observation.Open, High = observation.High, Low = observation.Low, Close = observation.Close,
                Volume = observation.Volume, TradeCount = observation.TradeCount,
                PriceVolumeSum = observation.PriceVolumeSum, ObservationId = observation.ObservationId,
                FirstSourceSequence = observation.FirstSourceSequence,
                LastSourceSequence = observation.LastSourceSequence,
                FirstMarketEventUtc = observation.FirstMarketEventUtc,
                LastMarketEventUtc = observation.LastMarketEventUtc,
                IsComplete = observation.IsComplete, IsValid = observation.IsValid
            }, cancellationToken).ConfigureAwait(false);
        }
        var entityId = new FuturesAnalyticsObservationEntityId(
            observation.MarketSeriesIdentity, observation.TimeFrame);
        var @event = new FuturesAnalyticsObservationClosedRealtimeEvent
        {
            Subject = new ActorSubject(ActorType.Realtime,
                FuturesAnalyticsObservationClosedRealtimeEvent.Actor,
                FuturesAnalyticsObservationClosedRealtimeEvent.Verb,
                entityId.Format()),
            Id = observation.ObservationId.Value,
            EntityId = entityId,
            AggregateId = entityId.Format(),
            EventSource = nameof(FuturesAnalyticsObservationRealtimeProjector),
            ReceivedOn = timeProvider.GetUtcNow().UtcDateTime,
            Observation = observation
        };
        await context.SendAsync<FuturesAnalyticsObservationClosedRealtimeEvent,
            FuturesAnalyticsObservationEntityId>(@event).ConfigureAwait(false);
        return true;
    }
}
