using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Realtime.Projector;

public sealed class FuturesMacdSignalRealtimeProjector(
    IDbContextFactory dbFactory,
    ILogger<FuturesMacdSignalRealtimeProjector> logger)
    : BaseRealtimeProjector<FuturesMacdSignalRealtimeActor>(logger)
{
    readonly ImmutableArray<RealtimeProjectionDescriptor> _descriptors =
    [
        Describe<FuturesMacdSignalGeneratedEvent, FuturesMacdSignalGeneratedCompleteEvent,
            FuturesMacdSignalGeneratedFailEvent, FuturesMacdSignalEntityId>(
            e => dbFactory.MarketDataDb.InsertFuturesMacdSignalAsync(e.FuturesMacdSignal))
    ];

    public override string ActorName => FuturesMacdSignalRealtimeActor.ActorName;
    public override string ProjectorName => nameof(FuturesMacdSignalRealtimeProjector);
    public override IReadOnlyCollection<RealtimeProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
