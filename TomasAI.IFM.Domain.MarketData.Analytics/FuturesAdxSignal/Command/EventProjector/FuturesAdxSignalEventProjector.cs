using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.EventProjector;

public sealed class FuturesAdxSignalEventProjector(
    IDbContextFactory dbFactory, IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource, IBlackboardService blackboardService,
    ILogger<FuturesAdxSignalEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesAdxSignalCommandActor>(durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesAdxSignalGeneratedEvent, FuturesAdxSignalGeneratedCompleteEvent, FuturesAdxSignalGeneratedFailEvent, FuturesAdxSignalEntityId>(
            (Func<FuturesAdxSignalGeneratedEvent, Task>)(async e => { await dbFactory.MarketDataDb.InsertFuturesAdxSignalAsync(e.FuturesAdxSignal).ConfigureAwait(false); RegimeDiscoverySignalCacheAdapter.Publish(e.FuturesAdxSignal); })),
        Describe<FuturesAdxDailySignalGeneratedEvent, FuturesAdxDailySignalGeneratedCompleteEvent, FuturesAdxDailySignalGeneratedFailEvent, FuturesAdxDailySignalEntityId>(
            (Func<FuturesAdxDailySignalGeneratedEvent, Task>)(async e => { await dbFactory.MarketDataDb.InsertFuturesAdxSignalAsync(e.FuturesAdxSignal).ConfigureAwait(false); RegimeDiscoverySignalCacheAdapter.Publish(e.FuturesAdxSignal); })),
        DescribeNotification<FuturesAdxSignalStartedEvent, FuturesAdxSignalEntityId>(useDurableReplay: false),
        DescribeNotification<FuturesAdxSignalStoppedEvent, FuturesAdxSignalEntityId>(useDurableReplay: false)
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => _descriptors.Select(static x => x.SourceEventType).ToArray();
}
