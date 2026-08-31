using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.EventProjector;

/// <summary>Projects Bollinger events to the existing Scylla read model.</summary>
public sealed class FuturesBbSignalEventProjector(IDbContextFactory dbFactory,
    IDurableReplayQueue durableReplayQueue, IEventSourceActorDbContext dbEventSource,
    IBlackboardService blackboardService, ILogger<FuturesBbSignalEventProjector> logger,
    EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesBbSignalCommandActor>(durableReplayQueue, dbEventSource,
        blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> descriptors =
    [
        Describe<FuturesBbSignalGeneratedEvent, FuturesBbSignalGeneratedCompleteEvent,
            FuturesBbSignalGeneratedFailEvent, FuturesTradeSessionBarEntityId>(
            (Func<FuturesBbSignalGeneratedEvent, Task>)(async e =>
            {
                await dbFactory.MarketDataDb.InsertFuturesBollingerBandSignalAsync(e.Signal).ConfigureAwait(false);
                RegimeDiscoverySignalCacheAdapter.Publish(e.Signal, e.Checkpoint);
            }))
    ];
    /// <inheritdoc />
    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => descriptors;
    /// <inheritdoc />
    public override IReadOnlyCollection<Type> ProjectedEventTypes => descriptors.Select(x => x.SourceEventType).ToArray();
}
