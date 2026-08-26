using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Command.EventProjector;

/// <summary>Projects committed trade-session bars to ScyllaDB before publishing terminal events.</summary>
public sealed class FuturesTradeSessionBarPublisherEventProjector(
    IHistoricalObservationStore store,
    IMarketSessionCalendar calendar,
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext eventSource,
    IBlackboardService blackboard,
    ILogger<FuturesTradeSessionBarPublisherEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesTradeSessionBarPublisherCommandActor>(
        durableReplayQueue, eventSource, blackboard, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> descriptors =
    [
        Describe<
            FuturesTradeSessionBarPublishedEvent,
            FuturesTradeSessionBarPublishedCompleteEvent,
            FuturesTradeSessionBarPublishedFailEvent,
            FuturesTradeSessionBarEntityId>(published => PersistAsync(published, store, calendar))
    ];

    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;

    /// <inheritdoc />
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();

    static async ValueTask PersistAsync(
        FuturesTradeSessionBarPublishedEvent published,
        IHistoricalObservationStore store,
        IMarketSessionCalendar calendar)
    {
        var bar = published.Bar;
        _ = await store.TryWriteObservationAsync(bar, CancellationToken.None).ConfigureAwait(false);
        if (bar.TimeFrame != TimeFrameType.Daily) return;
        var session = calendar.GetSession(bar.ValueDate);
        _ = await store.TryWriteRawEodAsync(new FuturesEodObservationReadModel
        {
            MarketSeriesIdentity = bar.MarketSeriesIdentity,
            ContractId = bar.ContractId,
            ValueDate = bar.ValueDate,
            SessionStartUtc = session.StartUtc,
            SessionEndUtc = session.EndUtc,
            Open = bar.Open,
            High = bar.High,
            Low = bar.Low,
            Close = bar.Close,
            Volume = bar.Volume,
            TradeCount = bar.TradeCount,
            PriceVolumeSum = bar.PriceVolumeSum,
            ObservationId = bar.ObservationId,
            FirstSourceSequence = bar.FirstSourceSequence,
            LastSourceSequence = bar.LastSourceSequence,
            FirstMarketEventUtc = bar.FirstMarketEventUtc,
            LastMarketEventUtc = bar.LastMarketEventUtc,
            IsComplete = bar.IsComplete,
            IsValid = bar.IsValid
        }, CancellationToken.None).ConfigureAwait(false);
    }
}
