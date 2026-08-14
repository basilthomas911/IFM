using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.EventProjector;

public sealed class FuturesEodDataEventProjector(
    IDbContextFactory dbFactory, IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource, IBlackboardService blackboardService,
    ILogger<FuturesEodDataEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesEodDataCommandActor>(durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesEodDataInsertedEvent, FuturesEodDataInsertedCompleteEvent, FuturesEodDataInsertedFailEvent, FuturesEodDataId>(
            e => dbFactory.MarketDataDb.InsertFuturesEodDataAsync(e.FuturesEodData)),
        Describe<VixFuturesEodDataInsertedEvent, VixFuturesEodDataInsertedCompleteEvent, VixFuturesEodDataInsertedFailEvent, FuturesEodDataId>(
            e => dbFactory.MarketDataDb.InsertVixFuturesEodDataAsync(e.VixFuturesTickData))
    ];
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => _descriptors.Select(static x => x.SourceEventType).ToArray();
}
