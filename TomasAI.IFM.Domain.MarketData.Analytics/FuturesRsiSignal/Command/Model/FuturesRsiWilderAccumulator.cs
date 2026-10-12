using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;

/// <summary>Advances immutable Wilder RSI state from one unique closed session bar.</summary>
public static class FuturesRsiWilderAccumulator
{
    /// <summary>Applies one observation and returns the new checkpoint and calculation values.</summary>
    public static FuturesRsiWilderResult Apply(
        FuturesRsiAccumulatorCheckpoint? checkpoint,
        FuturesTradeSessionBarReadModel observation,
        int periodLength)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(periodLength);
        if (checkpoint is not null && checkpoint.PeriodLength != periodLength)
            throw new InvalidOperationException("The RSI checkpoint period does not match the command period.");
        if (checkpoint?.LastObservationId.Value != Guid.Empty && checkpoint?.LastObservationId == observation.ObservationId)
            return FuturesRsiWilderResult.Ignored(checkpoint!, MarketObservationApplicationDisposition.Duplicate);
        if (checkpoint is not null
            && checkpoint.LastIntervalEndUtc != default
            && observation.IntervalEndUtc <= checkpoint.LastIntervalEndUtc)
            return FuturesRsiWilderResult.Ignored(checkpoint, MarketObservationApplicationDisposition.Stale);

        checkpoint ??= new() { PeriodLength = periodLength };
        var previousRsi = checkpoint.CurrentRsi;
        if (checkpoint.PreviousClose is null)
            return FirstObservation(checkpoint, observation, previousRsi);

        var change = observation.Close - checkpoint.PreviousClose.Value;
        var gain = Math.Max(change, 0m);
        var loss = Math.Max(-change, 0m);
        var count = checkpoint.ChangeCount + 1;
        var seedGain = checkpoint.SeedGainSum;
        var seedLoss = checkpoint.SeedLossSum;
        decimal? averageGain = checkpoint.AverageGain;
        decimal? averageLoss = checkpoint.AverageLoss;

        (seedGain, seedLoss, averageGain, averageLoss) = AdvanceAverages(
            seedGain, seedLoss, averageGain, averageLoss, gain, loss, count, periodLength);

        double? currentRsi = averageGain is not null && averageLoss is not null
            ? CalculateRsi(averageGain.Value, averageLoss.Value)
            : null;
        double? rs = (averageGain is null || averageLoss is null) switch
{
    true => null,
    false => (averageLoss.Value == 0m) switch
    {
        true => averageGain.Value == 0m ? 1d : double.MaxValue,
        false => (double)(averageGain.Value / averageLoss.Value)}};
        double? slope = currentRsi is not null && previousRsi is not null
            ? currentRsi.Value - previousRsi.Value
            : null;
        var next = checkpoint with
        {
            PreviousClose = observation.Close,
            SeedGainSum = seedGain,
            SeedLossSum = seedLoss,
            AverageGain = averageGain,
            AverageLoss = averageLoss,
            CurrentRsi = currentRsi,
            ChangeCount = count,
            LastObservationId = observation.ObservationId,
            LastSourceSequence = observation.LastSourceSequence,
            LastMarketEventUtc = observation.LastMarketEventUtc,
            LastIntervalEndUtc = observation.IntervalEndUtc,
            LastStreamEpochId = observation.StreamEpochId
        };
        return new(next, change, gain, loss, averageGain, averageLoss, rs, previousRsi, slope,
            currentRsi is not null && previousRsi is not null,
            MarketObservationApplicationDisposition.Applied);
    }

    /// <summary>Records the first close as the baseline; no price change or RSI is available yet.</summary>
    static FuturesRsiWilderResult FirstObservation(FuturesRsiAccumulatorCheckpoint checkpoint,
        FuturesTradeSessionBarReadModel observation, double? previousRsi)
    {
        var first = checkpoint with
        {
            PreviousClose = observation.Close,
            LastObservationId = observation.ObservationId,
            LastSourceSequence = observation.LastSourceSequence,
            LastMarketEventUtc = observation.LastMarketEventUtc,
            LastIntervalEndUtc = observation.IntervalEndUtc,
            LastStreamEpochId = observation.StreamEpochId
        };
        return new(first, 0m, 0m, 0m, null, null, null, previousRsi, null, false,
            MarketObservationApplicationDisposition.Applied);
    }

    /// <summary>Seeds from N changes; afterwards each average is (previous average ? (N ? 1) + current change) / N.</summary>
    static (decimal SeedGain, decimal SeedLoss, decimal? AverageGain, decimal? AverageLoss) AdvanceAverages(
        decimal seedGain, decimal seedLoss, decimal? averageGain, decimal? averageLoss,
        decimal gain, decimal loss, int count, int periodLength)
    {
        if (averageGain is null || averageLoss is null)
        {
            seedGain += gain;
            seedLoss += loss;
            if (count == periodLength)
            {
                averageGain = seedGain / periodLength;
                averageLoss = seedLoss / periodLength;
            }
        }
        else
        {
            averageGain = ((averageGain.Value * (periodLength - 1)) + gain) / periodLength;
            averageLoss = ((averageLoss.Value * (periodLength - 1)) + loss) / periodLength;
        }

        return (seedGain, seedLoss, averageGain, averageLoss);
    }

    static double CalculateRsi(decimal averageGain, decimal averageLoss) =>
        (averageLoss == 0m) switch
{
    true => averageGain == 0m ? 50d : 100d,
    false => 100d - (100d / (1d + (double)(averageGain / averageLoss)))};
}
