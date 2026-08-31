using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.EventProjector;

/// <summary>Projects EMA events to the existing Scylla read model.</summary>
public sealed class FuturesEmaSignalEventProjector(IDbContextFactory dbFactory,
    IDurableReplayQueue durableReplayQueue, IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService, ILogger<FuturesEmaSignalEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesEmaSignalCommandActor>(durableReplayQueue, dbEventSource,
        blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> descriptors =
    [
        Describe<FuturesEmaSignalGeneratedEvent, FuturesEmaSignalGeneratedCompleteEvent,
            FuturesEmaSignalGeneratedFailEvent, FuturesTradeSessionBarEntityId>(
            (Func<FuturesEmaSignalGeneratedEvent, Task>)(async e =>
            {
                await dbFactory.MarketDataDb.InsertFuturesEmaSignalAsync(e.Signal).ConfigureAwait(false);
                RegimeDiscoverySignalCacheAdapter.Publish(e.Signal, e.Checkpoint);
            }))
    ];
    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    /// <inheritdoc />
    public override IReadOnlyCollection<Type> ProjectedEventTypes => descriptors.Select(x => x.SourceEventType).ToArray();
}
