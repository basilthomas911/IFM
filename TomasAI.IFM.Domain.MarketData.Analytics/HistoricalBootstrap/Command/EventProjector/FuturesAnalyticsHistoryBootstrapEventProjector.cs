using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Historical;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Command.EventProjector;

/// <summary>Durably publishes accepted bootstrap Requested events to the bootstrap Event actor.</summary>
public sealed class FuturesAnalyticsHistoryBootstrapEventProjector(
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext eventSource,
    IBlackboardService blackboard,
    ILogger<FuturesAnalyticsHistoryBootstrapEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesAnalyticsHistoryBootstrapCommandActor>(
        durableReplayQueue, eventSource, blackboard, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> descriptors =
    [
        DescribeNotification<
            FuturesAnalyticsHistoryBootstrapRequestedEvent,
            FuturesAnalyticsHistoryBootstrapEntityId>(useDurableReplay: true)
    ];

    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;

    /// <inheritdoc />
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        descriptors.Select(static value => value.SourceEventType).ToArray();
}
