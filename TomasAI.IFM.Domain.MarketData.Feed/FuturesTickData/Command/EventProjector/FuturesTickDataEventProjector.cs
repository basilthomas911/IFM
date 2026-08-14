using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Command.EventProjector;

public sealed class FuturesTickDataEventProjector(
    IDbContextFactory dbFactory, IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource, IBlackboardService blackboardService,
    ILogger<FuturesTickDataEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesTickDataCommandActor>(durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        DescribeNotification<FuturesTickDataStreamingStartedEvent, FuturesTickDataStreamingId>(useDurableReplay: false),
        DescribeNotification<FuturesTickDataStreamingStoppedEvent, FuturesTickDataStreamingId>(useDurableReplay: false),
        Describe<FuturesTickDataInsertedEvent, FuturesTickDataInsertedCompleteEvent, FuturesTickDataInsertedFailEvent, FuturesTickDataId>(
            (e, context) => dbFactory.MarketDataDb.InsertFuturesTickDataAsync(
                e.TickData with
                {
                    TickId = e.TickData.TickId > 0 ? e.TickData.TickId : context.EventId
                }))
    ];
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => _descriptors.Select(static x => x.SourceEventType).ToArray();
}
