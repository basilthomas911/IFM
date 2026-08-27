using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.EventProjector;

public sealed class FuturesAtrSignalEventProjector(
    IDbContextFactory dbFactory, IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource, IBlackboardService blackboardService,
    ILogger<FuturesAtrSignalEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesAtrSignalCommandActor>(durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesAtrSignalGeneratedEvent, FuturesAtrSignalGeneratedCompleteEvent, FuturesAtrSignalGeneratedFailEvent, FuturesAtrSignalEntityId>(
            (Func<FuturesAtrSignalGeneratedEvent, Task>)(async e => { await dbFactory.MarketDataDb.InsertFuturesAtrSignalAsync(e.FuturesAtrSignal).ConfigureAwait(false); RegimeDiscoverySignalCacheAdapter.Publish(e.FuturesAtrSignal); })),
        Describe<FuturesAtrDailySignalGeneratedEvent, FuturesAtrDailySignalGeneratedCompleteEvent, FuturesAtrDailySignalGeneratedFailEvent, FuturesAtrDailySignalEntityId>(
            (Func<FuturesAtrDailySignalGeneratedEvent, Task>)(async e => { await dbFactory.MarketDataDb.InsertFuturesAtrSignalAsync(e.FuturesAtrSignal).ConfigureAwait(false); RegimeDiscoverySignalCacheAdapter.Publish(e.FuturesAtrSignal); })),
        DescribeNotification<FuturesAtrSignalStartedEvent, FuturesAtrSignalEntityId>(useDurableReplay: false),
        DescribeNotification<FuturesAtrSignalStoppedEvent, FuturesAtrSignalEntityId>(useDurableReplay: false)
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => _descriptors.Select(static x => x.SourceEventType).ToArray();
}
