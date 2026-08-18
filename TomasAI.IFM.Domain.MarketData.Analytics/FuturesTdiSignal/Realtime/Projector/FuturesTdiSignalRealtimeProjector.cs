using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Projector;

public sealed class FuturesTdiSignalRealtimeProjector(
    IDbContextFactory dbFactory,
    ILogger<FuturesTdiSignalRealtimeProjector> logger)
    : BaseRealtimeProjector<FuturesTdiSignalRealtimeActor>(logger)
{
    readonly ImmutableArray<RealtimeProjectionDescriptor> _descriptors =
    [
        Describe<FuturesTdiSignalGeneratedEvent, FuturesTdiSignalGeneratedCompleteEvent,
            FuturesTdiSignalGeneratedFailEvent, FuturesTdiSignalEntityId>(
            e => e.FuturesTdiSignal.SchemaVersion == FuturesTdiConfiguration.CurrentSchemaVersion
                ? new ValueTask(dbFactory.MarketDataDb.InsertFuturesTdiSignalAsync(e.FuturesTdiSignal))
                : ValueTask.CompletedTask)
    ];

    public override string ActorName => FuturesTdiSignalRealtimeActor.ActorName;
    public override string ProjectorName => nameof(FuturesTdiSignalRealtimeProjector);
    public override IReadOnlyCollection<RealtimeProjectionDescriptor> ProjectionDescriptors => _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
