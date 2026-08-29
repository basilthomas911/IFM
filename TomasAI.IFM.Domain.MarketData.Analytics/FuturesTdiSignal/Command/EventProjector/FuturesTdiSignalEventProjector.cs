using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Framework.Messaging.Nats;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.EventProjector;

public sealed class FuturesTdiSignalEventProjector(
    IDbContextFactory dbFactory, IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource, IBlackboardService blackboardService,
    ILogger<FuturesTdiSignalEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesTdiSignalCommandActor>(durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesTdiSignalGeneratedEvent, FuturesTdiSignalGeneratedCompleteEvent, FuturesTdiSignalGeneratedFailEvent, FuturesTdiSignalEntityId>(
            (Func<FuturesTdiSignalGeneratedEvent, Task>)(async e =>
            {
                if (e.FuturesTdiSignal.SchemaVersion != FuturesTdiConfiguration.CurrentSchemaVersion)
                    return;
                await dbFactory.MarketDataDb.InsertFuturesTdiSignalAsync(e.FuturesTdiSignal).ConfigureAwait(false);
                RegimeDiscoverySignalCacheAdapter.Publish(e.FuturesTdiSignal);
            }))
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => _descriptors.Select(static x => x.SourceEventType).ToArray();
}
