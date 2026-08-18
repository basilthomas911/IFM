using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Realtime.Projector;

/// <summary>Persists each realtime RSI result once and never schedules replay or retry.</summary>
public sealed class FuturesRsiSignalRealtimeProjector(
    IDbContextFactory dbFactory,
    ILogger<FuturesRsiSignalRealtimeProjector> logger)
    : BaseRealtimeProjector<FuturesRsiSignalRealtimeActor>(logger)
{
    readonly ImmutableArray<RealtimeProjectionDescriptor> _descriptors =
    [
        Describe<FuturesRsiSignalGeneratedEvent, FuturesRsiSignalGeneratedCompleteEvent,
            FuturesRsiSignalGeneratedFailEvent, FuturesRsiSignalEntityId>(
            e => dbFactory.MarketDataDb.InsertFuturesRsiSignalAsync(e.FuturesRsiSignal))
    ];

    public override string ActorName => FuturesRsiSignalRealtimeActor.ActorName;
    public override string ProjectorName => nameof(FuturesRsiSignalRealtimeProjector);
    public override IReadOnlyCollection<RealtimeProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
