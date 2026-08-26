using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Historical;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Command.EventProjector;

/// <summary>Durably publishes accepted data load Requested events to the data load Event actor.</summary>
public sealed class FuturesAnalyticsHistoricalDataLoaderEventProjector(
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext eventSource,
    IBlackboardService blackboard,
    ILogger<FuturesAnalyticsHistoricalDataLoaderEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesAnalyticsHistoricalDataLoaderCommandActor>(
        durableReplayQueue, eventSource, blackboard, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> descriptors =
    [
        DescribeNotification<
            FuturesAnalyticsHistoricalDataLoaderRequestedEvent,
            FuturesAnalyticsHistoricalDataLoaderEntityId>(useDurableReplay: true)
    ];

    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;

    /// <inheritdoc />
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        descriptors.Select(static value => value.SourceEventType).ToArray();
}
