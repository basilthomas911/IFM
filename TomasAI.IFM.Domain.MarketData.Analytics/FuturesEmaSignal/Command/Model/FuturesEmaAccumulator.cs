using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Model;

/// <summary>Advances the event-sourced EMA10/20/50/200 family.</summary>
public static class FuturesEmaAccumulator
{
    /// <summary>Applies one immutable close and returns the next checkpoint and signal.</summary>
    public static FuturesEmaAccumulatorResult Apply(
        FuturesEmaAccumulatorCheckpoint? checkpoint,
        FuturesTradeSessionBarReadModel observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        checkpoint ??= new();
        if (checkpoint.LastObservationId.Value != Guid.Empty && checkpoint.LastObservationId == observation.ObservationId)
            throw new InvalidOperationException($"Observation {observation.ObservationId} has already been applied.");
        if (observation.LastSourceSequence < checkpoint.LastSourceSequence)
            throw new InvalidOperationException("A stale observation cannot advance EMA state.");

        var count = checkpoint.Count + 1;
        var value10 = Advance(10, count, observation.Close, checkpoint.Seed10, checkpoint.Ema10);
        var value20 = Advance(20, count, observation.Close, checkpoint.Seed20, checkpoint.Ema20);
        var value50 = Advance(50, count, observation.Close, checkpoint.Seed50, checkpoint.Ema50);
        var value200 = Advance(200, count, observation.Close, checkpoint.Seed200, checkpoint.Ema200);
        var next = new FuturesEmaAccumulatorCheckpoint
        {
            Count = count,
            Seed10 = value10.Seed,
            Seed20 = value20.Seed,
            Seed50 = value50.Seed,
            Seed200 = value200.Seed,
            Ema10 = value10.Current,
            Ema20 = value20.Current,
            Ema50 = value50.Current,
            Ema200 = value200.Current,
            LastObservationId = observation.ObservationId,
            LastSourceSequence = observation.LastSourceSequence
        };
        var signal = new FuturesEmaSignalReadModel
        {
            Metadata = MarketAnalyticsSignalMetadataFactory.Create(
                observation, MarketAnalyticsSignalKind.Ema, "ema-10-20-50-200-v1", "ema-sma-seed-v1"),
            Price = observation.Close,
            Ema10 = value10.Current,
            PreviousEma10 = value10.Previous,
            Ema10Slope = Slope(value10),
            Ema20 = value20.Current,
            PreviousEma20 = value20.Previous,
            Ema20Slope = Slope(value20),
            Ema50 = value50.Current,
            PreviousEma50 = value50.Previous,
            Ema50Slope = Slope(value50),
            Ema200 = value200.Current,
            PreviousEma200 = value200.Previous,
            Ema200Slope = Slope(value200),
            IsWarm = value200.Current is not null && value200.Previous is not null
        };
        return new(next, signal);
    }

    static EmaValue Advance(int period, int count, decimal close, decimal seed, decimal? current)
    {
        var previous = current;
        if (count <= period)
        {
            seed += close;
            if (count == period) current = seed / period;
        }
        else
        {
            var multiplier = 2m / (period + 1m);
            current = ((close - current!.Value) * multiplier) + current.Value;
        }
        return new(seed, current, previous);
    }

    static decimal? Slope(EmaValue value) =>
        value.Current is not null && value.Previous is not null
            ? value.Current - value.Previous
            : null;

    readonly record struct EmaValue(decimal Seed, decimal? Current, decimal? Previous);
}

/// <summary>Contains one EMA state transition.</summary>
public sealed record FuturesEmaAccumulatorResult(
    FuturesEmaAccumulatorCheckpoint Checkpoint,
    FuturesEmaSignalReadModel Signal);
