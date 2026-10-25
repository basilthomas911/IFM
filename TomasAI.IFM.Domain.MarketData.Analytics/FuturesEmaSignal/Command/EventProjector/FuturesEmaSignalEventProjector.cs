using TomasAI.IFM.Domain.MarketData.Analytics.Persistence;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.EventProjector;

/// <summary>Projects EMA events to the configured read model after the source transaction.</summary>
public sealed class FuturesEmaSignalEventProjector(IDbContextFactory dbFactory,
    IDurableReplayQueue durableReplayQueue, IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService, ILogger<FuturesEmaSignalEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null,
    IPostgresEventTransaction? transactions = null)
    : ConventionalEventProjector<FuturesEmaSignalCommandActor>(durableReplayQueue, dbEventSource,
        blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> descriptors =
    [
        Describe<FuturesEmaSignalGeneratedEvent, FuturesEmaSignalGeneratedCompleteEvent,
            FuturesEmaSignalGeneratedFailEvent, FuturesTradeSessionBarEntityId>(
            (Func<FuturesEmaSignalGeneratedEvent, Task>)(async e =>
            {
                if (!string.Equals(dbFactory.MarketDataDb.ProviderName,"System.Data.Postgres",StringComparison.Ordinal)) await dbFactory.MarketDataDb.InsertFuturesEmaSignalAsync(e.FuturesEmaSignal).ConfigureAwait(false);
                RegimeDiscoverySignalCacheAdapter.Publish(e.FuturesEmaSignal, e.FuturesEmaCheckpoint);
            }))
            .WithReadModelRebuild((source,token) => AcceptedSignalRebuild.MaterializeAsync(dbFactory,transactions,source,token))
    ];
    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    /// <inheritdoc />
}
