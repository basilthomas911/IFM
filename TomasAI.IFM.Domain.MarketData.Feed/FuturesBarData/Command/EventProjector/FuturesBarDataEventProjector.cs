using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesBarData.Command.EventProjector;

public sealed class FuturesBarDataEventProjector(
    IDbContextFactory dbFactory, IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource, IBlackboardService blackboardService,
    ILogger<FuturesBarDataEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesBarDataCommandActor>(durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesBarDataStreamingStartedEvent, FuturesBarDataStreamingStartedCompleteEvent, FuturesBarDataStreamingStartedFailEvent, FuturesBarDataStreamingId>(
            static _ => Task.CompletedTask, useDurableReplay: false),
        Describe<FuturesBarDataStreamingStoppedEvent, FuturesBarDataStreamingStoppedCompleteEvent, FuturesBarDataStreamingStoppedFailEvent, FuturesBarDataStreamingId>(
            static _ => Task.CompletedTask, useDurableReplay: false),
        Describe<FuturesBarDataInsertedEvent, FuturesBarDataInsertedCompleteEvent, FuturesBarDataInsertedFailEvent, FuturesBarDataId>(
            e => dbFactory.MarketDataDb.InsertFuturesBarDataAsync(e.FuturesBarData)),
        Describe<FuturesBarDataDeletedEvent, FuturesBarDataDeletedCompleteEvent, FuturesBarDataDeletedFailEvent, FuturesBarDataId>(
            e => dbFactory.MarketDataDb.DeleteFuturesBarDataAsync(e.BarDataId))
    ];
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => _descriptors.Select(static x => x.SourceEventType).ToArray();
}
