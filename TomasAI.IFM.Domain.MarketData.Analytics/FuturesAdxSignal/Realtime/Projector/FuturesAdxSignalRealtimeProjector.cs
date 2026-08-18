using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Realtime.Projector;

public sealed class FuturesAdxSignalRealtimeProjector(
    IDbContextFactory dbFactory,
    ILogger<FuturesAdxSignalRealtimeProjector> logger)
    : BaseRealtimeProjector<FuturesAdxSignalRealtimeActor>(logger)
{
    readonly ImmutableArray<RealtimeProjectionDescriptor> _descriptors =
    [
        Describe<FuturesAdxSignalGeneratedEvent, FuturesAdxSignalGeneratedCompleteEvent,
            FuturesAdxSignalGeneratedFailEvent, FuturesAdxSignalEntityId>(
            e => dbFactory.MarketDataDb.InsertFuturesAdxSignalAsync(e.FuturesAdxSignal))
    ];

    public override string ActorName => FuturesAdxSignalRealtimeActor.ActorName;
    public override string ProjectorName => nameof(FuturesAdxSignalRealtimeProjector);
    public override IReadOnlyCollection<RealtimeProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
