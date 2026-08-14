using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.EventProjector;

public sealed class FuturesRsiSignalEventProjector(
    IDbContextFactory dbFactory, IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource, IBlackboardService blackboardService,
    ILogger<FuturesRsiSignalEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesRsiSignalCommandActor>(durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesRsiSignalGeneratedEvent, FuturesRsiSignalGeneratedCompleteEvent, FuturesRsiSignalGeneratedFailEvent, FuturesRsiSignalEntityId>(
            e => dbFactory.MarketDataDb.InsertFuturesRsiSignalAsync(e.FuturesRsiSignal)),
        Describe<FuturesRsiDailySignalGeneratedEvent, FuturesRsiDailySignalGeneratedCompleteEvent, FuturesRsiDailySignalGeneratedFailEvent, FuturesRsiDailySignalEntityId>(
            e => dbFactory.MarketDataDb.InsertFuturesRsiSignalAsync(e.FuturesRsiSignal)),
        DescribeNotification<FuturesRsiDailySignalsGeneratedEvent, FuturesRsiDailySignalEntityId>(),
        DescribeNotification<FuturesRsiSignalsGeneratedEvent, FuturesRsiSignalEntityId>(),
        DescribeNotification<FuturesRsiSignalStartedEvent, FuturesRsiSignalEntityId>(useDurableReplay: false),
        DescribeNotification<FuturesRsiSignalStoppedEvent, FuturesRsiSignalEntityId>(useDurableReplay: false)
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => _descriptors.Select(static x => x.SourceEventType).ToArray();
}
