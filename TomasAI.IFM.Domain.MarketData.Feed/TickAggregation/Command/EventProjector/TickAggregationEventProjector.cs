using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Command.Actor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Command.EventProjector;

public sealed class TickAggregationEventProjector(
    IDurableReplayQueue durableReplayQueue, IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService, ILogger<TickAggregationEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<TickAggregationCommandActor>(durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        DescribeNotification<FuturesTickTradeDataInsertedEvent, TickDataEntityId>(),
        DescribeNotification<FuturesTickQuoteDataInsertedEvent, TickDataEntityId>()
    ];
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => _descriptors.Select(static x => x.SourceEventType).ToArray();
}
