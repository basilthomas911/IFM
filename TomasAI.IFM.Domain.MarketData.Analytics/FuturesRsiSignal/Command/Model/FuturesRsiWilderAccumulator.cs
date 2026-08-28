using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

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
        if (periodLength <= 0) throw new ArgumentOutOfRangeException(nameof(periodLength));
        if (checkpoint is not null && checkpoint.PeriodLength != periodLength)
            throw new InvalidOperationException("The RSI checkpoint period does not match the command period.");
        if (checkpoint?.LastObservationId.Value != Guid.Empty && checkpoint?.LastObservationId == observation.ObservationId)
            throw new InvalidOperationException($"Observation {observation.ObservationId} has already been applied.");
        if (checkpoint is not null && observation.LastSourceSequence < checkpoint.LastSourceSequence)
            throw new InvalidOperationException("A stale observation cannot advance RSI state.");

        checkpoint ??= new() { PeriodLength = periodLength };
        var previousRsi = checkpoint.CurrentRsi;
        if (checkpoint.PreviousClose is null)
        {
            var first = checkpoint with
            {
                PreviousClose = observation.Close,
                LastObservationId = observation.ObservationId,
                LastSourceSequence = observation.LastSourceSequence,
                LastMarketEventUtc = observation.LastMarketEventUtc
            };
            return new(first, 0m, 0m, 0m, null, null, null, previousRsi, null, false);
        }

        var change = observation.Close - checkpoint.PreviousClose.Value;
        var gain = Math.Max(change, 0m);
        var loss = Math.Max(-change, 0m);
        var count = checkpoint.ChangeCount + 1;
        var seedGain = checkpoint.SeedGainSum;
        var seedLoss = checkpoint.SeedLossSum;
        decimal? averageGain = checkpoint.AverageGain;
        decimal? averageLoss = checkpoint.AverageLoss;

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

        double? currentRsi = averageGain is not null && averageLoss is not null
            ? CalculateRsi(averageGain.Value, averageLoss.Value)
            : null;
        double? rs = averageGain is null || averageLoss is null
            ? null
            : averageLoss.Value == 0m
                ? averageGain.Value == 0m ? 1d : double.MaxValue
                : (double)(averageGain.Value / averageLoss.Value);
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
            LastMarketEventUtc = observation.LastMarketEventUtc
        };
        return new(next, change, gain, loss, averageGain, averageLoss, rs, previousRsi, slope,
            currentRsi is not null && previousRsi is not null);
    }

    static double CalculateRsi(decimal averageGain, decimal averageLoss) =>
        averageLoss == 0m
            ? averageGain == 0m ? 50d : 100d
            : 100d - (100d / (1d + (double)(averageGain / averageLoss)));
}

/// <summary>Contains one Wilder RSI transition result.</summary>
public sealed record FuturesRsiWilderResult(
    FuturesRsiAccumulatorCheckpoint Checkpoint,
    decimal PriceChange,
    decimal PriceGain,
    decimal PriceLoss,
    decimal? AverageGain,
    decimal? AverageLoss,
    double? RelativeStrength,
    double? PreviousRsi,
    double? Slope,
    bool IsWarm);
