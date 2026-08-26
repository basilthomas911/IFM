using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketSignals.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Indicators;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Observation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketSignals.Realtime.Projector;

/// <summary>Persists the ordered EMA, Bollinger, and ATR projections before publishing completion.</summary>
public sealed class FuturesRegimeIndicatorRealtimeProjector(
    IDbContextFactory dbFactory,
    ILogger<FuturesRegimeIndicatorRealtimeProjector> logger)
    : BaseRealtimeProjector<FuturesRegimeIndicatorRealtimeActor>(logger)
{
    readonly ImmutableArray<RealtimeProjectionDescriptor> descriptors =
    [
        Describe<FuturesRegimeIndicatorsGeneratedRealtimeEvent,
            FuturesRegimeIndicatorsGeneratedCompleteRealtimeEvent,
            FuturesRegimeIndicatorsGeneratedFailRealtimeEvent,
            FuturesTradeSessionBarEntityId>(async (generated, cancellationToken) =>
        {
            await dbFactory.MarketDataDb.InsertFuturesEmaSignalAsync(
                generated.Snapshot.Ema, cancellationToken).ConfigureAwait(false);
            await dbFactory.MarketDataDb.InsertFuturesBollingerBandSignalAsync(
                generated.Snapshot.BollingerBand, cancellationToken).ConfigureAwait(false);
        })
    ];

    /// <inheritdoc />
    public override string ActorName => FuturesRegimeIndicatorRealtimeActor.ActorName;
    /// <inheritdoc />
    public override string ProjectorName => nameof(FuturesRegimeIndicatorRealtimeProjector);
    /// <inheritdoc />
    public override IReadOnlyCollection<RealtimeProjectionDescriptor> ProjectionDescriptors => descriptors;
    /// <inheritdoc />
    public override IReadOnlyCollection<Type> ProjectedEventTypes =>
        descriptors.Select(static descriptor => descriptor.SourceEventType).ToArray();
}
