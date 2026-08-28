using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.Model;

/// <summary>Advances event-sourced EMA-centered BB10/20 calculation state.</summary>
public static class FuturesBbAccumulator
{
    /// <summary>Applies a same-observation EMA signal and returns the next checkpoint and bands.</summary>
    public static FuturesBbAccumulatorResult Apply(
        FuturesBbAccumulatorCheckpoint? checkpoint,
        FuturesTradeSessionBarReadModel observation,
        FuturesEmaSignalReadModel ema)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(ema);
        if (ema.Metadata.ObservationId != observation.ObservationId)
            throw new InvalidOperationException("EMA and Bollinger observation identities must match.");
        checkpoint ??= new();
        if (checkpoint.LastObservationId.Value != Guid.Empty && checkpoint.LastObservationId == observation.ObservationId)
            throw new InvalidOperationException($"Observation {observation.ObservationId} has already been applied.");
        if (observation.LastSourceSequence < checkpoint.LastSourceSequence)
            throw new InvalidOperationException("A stale observation cannot advance Bollinger state.");

        var closes = checkpoint.Closes.Append(observation.Close).TakeLast(20).ToArray();
        decimal? sd10 = closes.Length >= 10 ? PopulationStandardDeviation(closes.TakeLast(10)) : null;
        decimal? sd20 = closes.Length >= 20 ? PopulationStandardDeviation(closes) : null;
        decimal? width10 = sd10 is not null && ema.Ema10 is not null ? 4m * sd10 : null;
        decimal? width20 = sd20 is not null && ema.Ema20 is not null ? 4m * sd20 : null;
        decimal? baseline = checkpoint.CompletedWidths20.Length == 20
            ? checkpoint.CompletedWidths20.Average()
            : null;
        var signal = new FuturesBbSignalReadModel
        {
            Metadata = MarketAnalyticsSignalMetadataFactory.Create(
                observation, MarketAnalyticsSignalKind.BollingerBand,
                "bb-10-20-ema-center-population-v1", "bb-ema-center-population-v1"),
            Price = observation.Close,
            Ema10Center = ema.Ema10,
            StandardDeviation10 = sd10,
            Upper10 = ema.Ema10 + (2m * sd10),
            Lower10 = ema.Ema10 - (2m * sd10),
            Width10 = width10,
            Position10 = Position(observation.Close, ema.Ema10, sd10),
            Ema20Center = ema.Ema20,
            StandardDeviation20 = sd20,
            Upper20 = ema.Ema20 + (2m * sd20),
            Lower20 = ema.Ema20 - (2m * sd20),
            Width20 = width20,
            Position20 = Position(observation.Close, ema.Ema20, sd20),
            Width20Baseline = baseline,
            Width20Ratio = width20 is > 0 && baseline is > 0 ? width20 / baseline : null,
            IsWarm = width20 is > 0 && baseline is > 0
        };
        var widths = width20 is > 0
            ? checkpoint.CompletedWidths20.Append(width20.Value).TakeLast(20).ToArray()
            : [.. checkpoint.CompletedWidths20];
        return new(new()
        {
            Closes = closes,
            CompletedWidths20 = widths,
            LastObservationId = observation.ObservationId,
            LastSourceSequence = observation.LastSourceSequence
        }, signal);
    }

    static decimal? Position(decimal close, decimal? center, decimal? standardDeviation)
    {
        if (center is null || standardDeviation is not > 0) return null;
        var lower = center.Value - (2m * standardDeviation.Value);
        return (close - lower) / (4m * standardDeviation.Value);
    }

    static decimal PopulationStandardDeviation(IEnumerable<decimal> values)
    {
        var data = values.ToArray();
        var mean = data.Average();
        var variance = data.Average(value => (value - mean) * (value - mean));
        return (decimal)Math.Sqrt((double)variance);
    }
}

/// <summary>Contains one Bollinger state transition.</summary>
public sealed record FuturesBbAccumulatorResult(
    FuturesBbAccumulatorCheckpoint Checkpoint,
    FuturesBbSignalReadModel Signal);
