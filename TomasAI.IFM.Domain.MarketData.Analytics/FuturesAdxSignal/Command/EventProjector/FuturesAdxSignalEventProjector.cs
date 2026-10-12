using TomasAI.IFM.Domain.MarketData.Analytics.Persistence;
using TomasAI.IFM.Application.Storage.EventSourceDb;
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
    ILogger<FuturesAdxSignalEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions = null,
    IPostgresEventTransaction? transactions = null)
    : ConventionalEventProjector<FuturesAdxSignalCommandActor>(durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesAdxSignalGeneratedEvent, FuturesAdxSignalGeneratedCompleteEvent, FuturesAdxSignalGeneratedFailEvent, FuturesAdxSignalEntityId>(
            (Func<FuturesAdxSignalGeneratedEvent, Task>)(async e => { if (!string.Equals(dbFactory.MarketDataDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal)) {
await dbFactory.MarketDataDb.InsertFuturesAdxSignalAsync(e.FuturesAdxSignal).ConfigureAwait(false);}
RegimeDiscoverySignalCacheAdapter.Publish(e.FuturesAdxSignal); }))
            .WithReadModelRebuild((source,token) => AcceptedSignalRebuild.MaterializeAsync(dbFactory,transactions,source,token)),
        Describe<FuturesAdxDailySignalGeneratedEvent, FuturesAdxDailySignalGeneratedCompleteEvent, FuturesAdxDailySignalGeneratedFailEvent, FuturesAdxDailySignalEntityId>(
            (Func<FuturesAdxDailySignalGeneratedEvent, Task>)(async e => { if (!string.Equals(dbFactory.MarketDataDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal)) {
await dbFactory.MarketDataDb.InsertFuturesAdxSignalAsync(e.FuturesAdxSignal).ConfigureAwait(false);}
RegimeDiscoverySignalCacheAdapter.Publish(e.FuturesAdxSignal); }))
            .WithReadModelRebuild((source,token) => AcceptedSignalRebuild.MaterializeAsync(dbFactory,transactions,source,token)),
        DescribeNotification<FuturesAdxSignalStartedEvent, FuturesAdxSignalEntityId>(useDurableReplay: false),
        DescribeNotification<FuturesAdxSignalStoppedEvent, FuturesAdxSignalEntityId>(useDurableReplay: false)
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
}
