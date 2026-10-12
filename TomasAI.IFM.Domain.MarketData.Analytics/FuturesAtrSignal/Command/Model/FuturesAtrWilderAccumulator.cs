using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Model;

/// <summary>Applies closed OHLC observations to an immutable Wilder ATR checkpoint.</summary>
public static class FuturesAtrWilderAccumulator
{
    const int BaselineLength = 20;

    /// <summary>Applies one newer observation and returns the resulting calculation and checkpoint.</summary>
    public static bool TryApply(
        FuturesTradeSessionBarReadModel observation,
        int periodLength,
        FuturesAtrAccumulatorCheckpoint? checkpoint,
        out FuturesAtrWilderResult result)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(periodLength);
        checkpoint ??= FuturesAtrAccumulatorCheckpoint.Empty(periodLength);
        if (checkpoint.PeriodLength is not (0) && checkpoint.PeriodLength != periodLength)
            throw new InvalidOperationException("The ATR checkpoint period does not match the command period.");
        if (IsDuplicateOrStale(observation, checkpoint))
        {
            result = default!;
            return false;
        }

        var trueRange = TrueRange(observation, checkpoint.PreviousClose);
        var seed = checkpoint.SeedTrueRanges.ToList();
        var completed = checkpoint.CompletedAtrValues.ToList();
        var previousAtr = checkpoint.CurrentAtr;
        var currentAtr = CalculateCurrentAtr(previousAtr, seed, trueRange, periodLength);

        decimal? baseline = completed.Count == BaselineLength ? completed.Average() : null;
        var ratio = currentAtr is not null && baseline is > 0
            ? currentAtr / baseline
            : null;
        if (currentAtr is not null)
        {
            completed.Add(currentAtr.Value);
            while (completed.Count > BaselineLength) completed.RemoveAt(0);
        }

        var next = checkpoint with
        {
            PeriodLength = periodLength,
            PreviousClose = observation.Close,
            SeedTrueRanges = currentAtr is null ? [.. seed] : [],
            CurrentAtr = currentAtr,
            CompletedAtrValues = [.. completed],
            LastObservationId = observation.ObservationId,
            LastSourceSequence = observation.LastSourceSequence,
            LastMarketEventUtc = observation.LastMarketEventUtc,
            LastIntervalEndUtc = observation.IntervalEndUtc,
            LastStreamEpochId = observation.StreamEpochId,
            ObservationCount = checked(checkpoint.ObservationCount + 1)
        };
        result = new FuturesAtrWilderResult(
            trueRange,
            currentAtr,
            previousAtr,
            baseline,
            ratio,
            currentAtr is not null,
            next);
        return true;
    }

    /// <summary>Seeds ATR with the mean of N true ranges; afterward applies (previous ATR ? (N ? 1) + current true range) / N.</summary>
    static decimal? CalculateCurrentAtr(decimal? previousAtr, List<decimal> seed, decimal trueRange, int periodLength)
    {
        if (previousAtr is not null)
            return ((previousAtr.Value * (periodLength - 1)) + trueRange) / periodLength;
        seed.Add(trueRange);
        return seed.Count == periodLength ? seed.Average() : null;
    }

    static decimal TrueRange(FuturesTradeSessionBarReadModel observation, decimal? previousClose)
    {
        var highLow = observation.High - observation.Low;
        if (previousClose is null) return highLow;
        return Math.Max(highLow, Math.Max(
            Math.Abs(observation.High - previousClose.Value),
            Math.Abs(observation.Low - previousClose.Value)));
    }

    static bool IsDuplicateOrStale(
        FuturesTradeSessionBarReadModel observation,
        FuturesAtrAccumulatorCheckpoint checkpoint)
    {
        if (checkpoint.LastObservationId.Value != Guid.Empty
            && observation.ObservationId == checkpoint.LastObservationId)
            return true;
        if (checkpoint.LastIntervalEndUtc != default)
            return observation.IntervalEndUtc <= checkpoint.LastIntervalEndUtc;
        return false;
    }
}
