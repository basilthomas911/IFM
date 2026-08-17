using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime.Projector;

/// <summary>Persists generated ITI observations once and never replays them.</summary>
public sealed class FuturesItiSignalRealtimeProjector(
    IDbContextFactory dbFactory,
    ILogger<FuturesItiSignalRealtimeProjector> logger)
    : BaseRealtimeProjector<FuturesItiSignalRealtimeActor>(logger)
{
    readonly ImmutableArray<RealtimeProjectionDescriptor> _descriptors =
    [
        Describe<
            FuturesItiSignalGeneratedEvent,
            FuturesItiSignalGeneratedCompleteEvent,
            FuturesItiSignalGeneratedFailEvent,
            FuturesItiSignalEntityId>(e => dbFactory.MarketDataDb.InsertFuturesItiSignalAsync(
                e.FuturesItiSignal
                    ?? throw new InvalidOperationException("FuturesItiSignal payload is required."))),
        Describe<
            FuturesTradeSignalUpdatedEvent,
            FuturesTradeSignalUpdatedCompleteEvent,
            FuturesTradeSignalUpdatedFailEvent,
            FuturesTradeSignalEntityId>(e => dbFactory.MarketDataDb.InsertFuturesTradeSignalAsync(
                e.FuturesTradeSignal
                    ?? throw new InvalidOperationException("FuturesTradeSignal payload is required.")))
    ];

    public override string ActorName => FuturesItiSignalRealtimeActor.ActorName;
    public override string ProjectorName => nameof(FuturesItiSignalRealtimeProjector);
    public override IReadOnlyCollection<RealtimeProjectionDescriptor> ProjectionDescriptors =>
        _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
