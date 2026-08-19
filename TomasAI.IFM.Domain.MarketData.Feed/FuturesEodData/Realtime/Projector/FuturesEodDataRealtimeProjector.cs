using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Projector;

/// <summary>
/// Applies rolling futures and VIX EOD observations once, without event-log or
/// projection replay infrastructure.
/// </summary>
public sealed class FuturesEodDataRealtimeProjector(
    IDbContextFactory dbFactory,
    ILogger<FuturesEodDataRealtimeProjector> logger)
    : BaseRealtimeProjector<FuturesEodDataRealtimeActor>(logger)
{
    readonly ImmutableArray<RealtimeProjectionDescriptor> _descriptors =
    [
        Describe<
            FuturesEodDataInsertedEvent,
            FuturesEodDataInsertedCompleteEvent,
            FuturesEodDataInsertedFailEvent,
            FuturesEodDataId>(e => dbFactory.MarketDataDb.InsertFuturesEodDataAsync(e.FuturesEodData)),
        Describe<
            FuturesEodSessionStatisticsUpdatedEvent,
            FuturesEodDataInsertedCompleteEvent,
            FuturesEodDataInsertedFailEvent,
            FuturesEodDataId>(e => dbFactory.MarketDataDb.UpdateFuturesEodSessionStatisticsAsync(
                e.FuturesEodData)),
        Describe<
            VixFuturesEodDataInsertedEvent,
            VixFuturesEodDataInsertedCompleteEvent,
            VixFuturesEodDataInsertedFailEvent,
            FuturesEodDataId>(e => dbFactory.MarketDataDb.InsertVixFuturesEodDataAsync(
                e.VixFuturesTickData,
                e.SessionStatistics))
    ];

    public override string ActorName => FuturesEodDataRealtimeActor.ActorName;
    public override string ProjectorName => nameof(FuturesEodDataRealtimeProjector);
    public override IReadOnlyCollection<RealtimeProjectionDescriptor> ProjectionDescriptors =>
        _descriptors;
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        _descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
