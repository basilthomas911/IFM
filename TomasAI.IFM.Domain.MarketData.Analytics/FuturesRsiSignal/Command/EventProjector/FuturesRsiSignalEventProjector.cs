using TomasAI.IFM.Domain.MarketData.Analytics.Persistence;
using TomasAI.IFM.Application.Storage.EventSourceDb;
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
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.EventProjector;

public sealed class FuturesRsiSignalEventProjector(
    IDbContextFactory dbFactory, IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource, IBlackboardService blackboardService,
    ILogger<FuturesRsiSignalEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions = null,
    IPostgresEventTransaction? transactions = null)
    : ConventionalEventProjector<FuturesRsiSignalCommandActor>(durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesRsiSignalGeneratedEvent, FuturesRsiSignalGeneratedCompleteEvent, FuturesRsiSignalGeneratedFailEvent, FuturesRsiSignalEntityId>(
            (Func<FuturesRsiSignalGeneratedEvent, Task>)(async e => { if (!string.Equals(dbFactory.MarketDataDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal)) {
await dbFactory.MarketDataDb.InsertFuturesRsiSignalAsync(e.FuturesRsiSignal).ConfigureAwait(false);}
RegimeDiscoverySignalCacheAdapter.Publish(e.FuturesRsiSignal); }))
            .WithReadModelRebuild((source,token) => AcceptedSignalRebuild.MaterializeAsync(dbFactory,transactions,source,token)),
        Describe<FuturesRsiDailySignalGeneratedEvent, FuturesRsiDailySignalGeneratedCompleteEvent, FuturesRsiDailySignalGeneratedFailEvent, FuturesRsiDailySignalEntityId>(
            (Func<FuturesRsiDailySignalGeneratedEvent, Task>)(async e => { if (!string.Equals(dbFactory.MarketDataDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal)) {
await dbFactory.MarketDataDb.InsertFuturesRsiSignalAsync(e.FuturesRsiSignal).ConfigureAwait(false);}
RegimeDiscoverySignalCacheAdapter.Publish(e.FuturesRsiSignal); }))
            .WithReadModelRebuild((source,token) => AcceptedSignalRebuild.MaterializeAsync(dbFactory,transactions,source,token)),
        DescribeNotification<FuturesRsiDailySignalsGeneratedEvent, FuturesRsiDailySignalEntityId>(),
        DescribeNotification<FuturesRsiSignalsGeneratedEvent, FuturesRsiSignalEntityId>(),
        DescribeNotification<FuturesRsiSignalStartedEvent, FuturesRsiSignalEntityId>(useDurableReplay: false),
        DescribeNotification<FuturesRsiSignalStoppedEvent, FuturesRsiSignalEntityId>(useDurableReplay: false)
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
}
