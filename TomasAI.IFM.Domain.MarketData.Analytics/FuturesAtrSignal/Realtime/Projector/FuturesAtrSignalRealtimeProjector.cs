using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Realtime.Projector;

public sealed class FuturesAtrSignalRealtimeProjector(
    IDbContextFactory dbFactory,
    ILogger<FuturesAtrSignalRealtimeProjector> logger)
    : BaseRealtimeProjector<FuturesAtrSignalRealtimeActor>(logger)
{
    readonly ImmutableArray<RealtimeProjectionDescriptor> _descriptors =
    [
        Describe<FuturesAtrSignalGeneratedEvent, FuturesAtrSignalGeneratedCompleteEvent,
            FuturesAtrSignalGeneratedFailEvent, FuturesAtrSignalEntityId>(
            e => dbFactory.MarketDataDb.InsertFuturesAtrSignalAsync(e.FuturesAtrSignal))
    ];

    public override string ActorName => FuturesAtrSignalRealtimeActor.ActorName;
    public override string ProjectorName => nameof(FuturesAtrSignalRealtimeProjector);
    public override IReadOnlyCollection<RealtimeProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
