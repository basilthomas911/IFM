using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.EventProjector;

public sealed class FuturesItiSignalEventProjector(
    IDbContextFactory dbFactory, IDurableReplayQueue durableReplayQueue,
    IEventSourceActorDbContext dbEventSource, IBlackboardService blackboardService,
    ILogger<FuturesItiSignalEventProjector> logger, EventProjectorReliabilityOptions? reliabilityOptions = null)
    : ConventionalEventProjector<FuturesItiSignalCommandActor>(durableReplayQueue, dbEventSource, blackboardService, logger, reliabilityOptions)
{
    readonly ImmutableArray<EventProjectionDescriptor> _descriptors =
    [
        Describe<FuturesItiSignalGeneratedEvent, FuturesItiSignalGeneratedCompleteEvent, FuturesItiSignalGeneratedFailEvent, FuturesItiSignalEntityId>(
            async (e, context) =>
            {
                var db = dbFactory.MarketDataDb;
                var signal = e.FuturesItiSignal
                    ?? throw new InvalidOperationException("FuturesItiSignal payload is required.");
                signal = signal with
                {
                    SequenceId = signal.SequenceId > 0 ? signal.SequenceId : context.EventId
                };
                await db.InsertFuturesItiSignalAsync(signal).ConfigureAwait(false);
                RegimeDiscoverySignalCacheAdapter.Publish(signal, signal.SequenceId,
                    e.CreatedOn == default ? e.ReceivedOn : e.CreatedOn, (decimal)e.VixFuturesPrice);
            })
    ];

    public override IReadOnlyCollection<EventProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes => _descriptors.Select(static x => x.SourceEventType).ToArray();
}
