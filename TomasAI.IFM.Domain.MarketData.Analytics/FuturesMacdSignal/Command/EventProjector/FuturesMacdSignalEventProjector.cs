using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.EventProjector;

public sealed class FuturesMacdSignalEventProjector(
    IDbContextFactory dbFactory, IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource, IBlackboardService blackboardService,
    ILogger<FuturesMacdSignalEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesMacdSignalCommandActor>(durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesMacdSignalGeneratedEvent, FuturesMacdSignalGeneratedCompleteEvent, FuturesMacdSignalGeneratedFailEvent, FuturesMacdSignalEntityId>(
            (Func<FuturesMacdSignalGeneratedEvent, Task>)(async e => { await dbFactory.MarketDataDb.InsertFuturesMacdSignalAsync(e.FuturesMacdSignal).ConfigureAwait(false); RegimeDiscoverySignalCacheAdapter.Publish(e.FuturesMacdSignal); })),
        Describe<FuturesMacdDailySignalGeneratedEvent, FuturesMacdDailySignalGeneratedCompleteEvent, FuturesMacdDailySignalGeneratedFailEvent, FuturesMacdDailySignalEntityId>(
            (Func<FuturesMacdDailySignalGeneratedEvent, Task>)(async e => { await dbFactory.MarketDataDb.InsertFuturesMacdSignalAsync(e.FuturesMacdSignal).ConfigureAwait(false); RegimeDiscoverySignalCacheAdapter.Publish(e.FuturesMacdSignal); })),
        DescribeNotification<FuturesMacdSignalStartedEvent, FuturesMacdSignalEntityId>(useDurableReplay: false),
        DescribeNotification<FuturesMacdSignalStoppedEvent, FuturesMacdSignalEntityId>(useDurableReplay: false)
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => _descriptors.Select(static x => x.SourceEventType).ToArray();
}
