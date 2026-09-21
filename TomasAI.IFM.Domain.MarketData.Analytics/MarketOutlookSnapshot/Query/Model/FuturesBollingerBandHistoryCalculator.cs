using TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesBbSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

namespace TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Query.Model;

/// <summary>Calculates a bounded Daily Bollinger history from normalized Databento EOD observations.</summary>
public static class FuturesBollingerBandHistoryCalculator
{
    /// <summary>
    /// Replays valid observations through the production EMA and Bollinger accumulators and returns
    /// the latest fully calculated signals before the current value date.
    /// </summary>
    /// <param name="observations">Normalized Databento EOD observations, in any order.</param>
    /// <param name="currentValueDate">The current chart value date, which is excluded from history.</param>
    /// <param name="maximumDays">The maximum number of prior trading-date signals to return.</param>
    /// <returns>Calculated Daily Bollinger signals in ascending value-date order.</returns>
    public static FuturesBbSignalReadModel[] Calculate(
        IEnumerable<FuturesEodObservationReadModel> observations,
        DateOnly currentValueDate,
        int maximumDays)
    {
        ArgumentNullException.ThrowIfNull(observations);
        if (maximumDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumDays));

        var ordered = observations
            .Where(observation => observation.IsComplete
                                  && observation.IsValid
                                  && observation.ValueDate < currentValueDate)
            .GroupBy(observation => observation.ValueDate)
            .Select(group => group
                .OrderByDescending(observation => observation.LastMarketEventUtc)
                .First())
            .OrderBy(observation => observation.ValueDate)
            .ToArray();

        FuturesEmaAccumulatorCheckpoint? emaCheckpoint = null;
        FuturesBbAccumulatorCheckpoint? bollingerCheckpoint = null;
        List<FuturesBbSignalReadModel> signals = [];
        foreach (var source in ordered)
        {
            var observation = FuturesEodObservationMapper.ToDailyBar(source);
            var emaResult = FuturesEmaAccumulator.Apply(emaCheckpoint, observation);
            emaCheckpoint = emaResult.Checkpoint;
            if (emaResult.Signal is not { } ema)
                continue;

            var bollingerResult = FuturesBbAccumulator.Apply(
                bollingerCheckpoint,
                observation,
                ema);
            bollingerCheckpoint = bollingerResult.Checkpoint;
            if (bollingerResult.Signal is
                {
                    Ema20Center: not null,
                    Upper20: not null,
                    Lower20: not null
                } signal)
                signals.Add(signal);
        }

        return signals.TakeLast(maximumDays).ToArray();
    }
}
