using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVxTermStructureSignal;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVxTermStructureSignal.Command.EventProjector;

/// <summary>Projects valid paired VX curve events to the Scylla read model.</summary>
public sealed class FuturesVxTermStructureSignalEventProjector(
    IDbContextFactory dbFactory,
    IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService,
    ILogger<FuturesVxTermStructureSignalEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesVxTermStructureSignalCommandActor>(
        durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> descriptors =
    [
        Describe<FuturesVxTermStructureSignalUpdatedEvent,
            FuturesVxTermStructureSignalUpdatedCompleteEvent,
            FuturesVxTermStructureSignalUpdatedFailEvent,
            FuturesVxTermStructureSignalEntityId>((Func<FuturesVxTermStructureSignalUpdatedEvent, Task>)(async e =>
            {
                if (e.Signal is null) return;
                await dbFactory.MarketDataDb.InsertFuturesVxTermStructureSignalAsync(e.Signal).ConfigureAwait(false);
                RegimeDiscoverySignalCacheAdapter.Publish(e.Signal);
            }))
    ];
    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    /// <inheritdoc />
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
