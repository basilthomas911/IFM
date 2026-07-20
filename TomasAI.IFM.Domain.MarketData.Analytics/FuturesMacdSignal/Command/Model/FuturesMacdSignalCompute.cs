using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesMacdSignal.Command.Model;

/// <summary>
/// Represents a model for analyzing futures trading signals based on MACD (Moving Average Convergence Divergence)
/// computed from RSI (Relative Strength Index) values.
/// </summary>
/// <remarks>This model processes an array of FuturesRsiSignalReadModel instances, computing the MACD line,
/// signal line, and histogram to determine trend direction and strength.</remarks>
public class FuturesMacdSignalCompute
{
    readonly FuturesMacdSignalReadModel? _macdSignal;
    int _signalPeriod;

    const int FastPeriod = 9;
    const int SlowPeriod = 26;

    public static bool Create(int periodLength, IReadOnlyCollection<FuturesMacdSignalReadModel> previousMacdSignals, out FuturesMacdSignalCompute model)
    {
        model = new(periodLength, previousMacdSignals);
        return true;
    }

    FuturesMacdSignalCompute(int periodLength,IReadOnlyCollection<FuturesMacdSignalReadModel> previousMacdSignals)
    {
        _signalPeriod = periodLength;
        _macdSignal = previousMacdSignals.LastOrDefault();
        // make sure signals are in ascending order...
        //var orderedSignals = futuresRsiSignals.OrderBy(e => e.Timestamp).ToArray();

        // compute MACD components from price values...
        var priceValues = previousMacdSignals.Select(e => (double)e.FuturesPrice).ToArray();
        var fastEma = ComputeEma(priceValues, FastPeriod);
        var slowEma = ComputeEma(priceValues, SlowPeriod);
        MacdLine = fastEma - slowEma;

        // compute signal line as EMA of MACD line series...
        var macdSeries = ComputeMacdSeries(priceValues, FastPeriod, SlowPeriod);
        SignalLine = ComputeEma(macdSeries, _signalPeriod);
        Histogram = MacdLine - SignalLine;
    }

    /// <summary>MACD line value (fast EMA minus slow EMA of RSI).</summary>
    public double MacdLine { get; private set; }

    /// <summary>Signal line value (EMA of MACD line series).</summary>
    public double SignalLine { get; private set; }

    /// <summary>Histogram value (MACD line minus signal line).</summary>
    public double Histogram { get; private set; }

    public FuturesTrendType TrendDirection
        => default(FuturesTrendType) switch
        {
            _ when Histogram > 0 && MacdLine > SignalLine => FuturesTrendType.UpTrending,
            _ when Histogram < 0 && MacdLine < SignalLine => FuturesTrendType.DownTrending,
            _ => FuturesTrendType.RangeBound
        };

    public FuturesTrendDirectionStrengthType TrendDirectionStrength()
    {
        var absHistogram = Math.Abs(Histogram);
        return absHistogram switch
        {
            >= 5.0 => FuturesTrendDirectionStrengthType.High,
            >= 2.0 => FuturesTrendDirectionStrengthType.Medium,
            _ => FuturesTrendDirectionStrengthType.Low
        };
    }

    static double ComputeEma(double[] values, int period)
    {
        if (values.Length == 0) return 0;
        var multiplier = 2.0 / (period + 1);
        var ema = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            ema = (values[i] - ema) * multiplier + ema;
        }
        return ema;
    }

    static double[] ComputeMacdSeries(double[] rsiValues, int fastPeriod, int slowPeriod)
    {
        if (rsiValues.Length == 0) return [];
        var fastMultiplier = 2.0 / (fastPeriod + 1);
        var slowMultiplier = 2.0 / (slowPeriod + 1);
        var fastEma = rsiValues[0];
        var slowEma = rsiValues[0];
        var result = new double[rsiValues.Length];
        result[0] = 0;
        for (int i = 1; i < rsiValues.Length; i++)
        {
            fastEma = (rsiValues[i] - fastEma) * fastMultiplier + fastEma;
            slowEma = (rsiValues[i] - slowEma) * slowMultiplier + slowEma;
            result[i] = fastEma - slowEma;
        }
        return result;
    }

    /// <summary>
    /// Indicates whether no prior MACD signal exists (initial state).
    /// </summary>
    internal bool IsSignalInitializing
        => _macdSignal is null;

    /// <summary>
    /// Indicates whether the current MACD signal is in an up-trending state.
    /// </summary>
    internal bool IsSignalUpTrending
        => TrendDirection == FuturesTrendType.UpTrending
           && (_macdSignal!.MACD == FuturesTrendDirectionType.UpTrending || _macdSignal.MACD == FuturesTrendDirectionType.TrendReversal);

    /// <summary>
    /// Indicates whether the current MACD signal is in a down-trending state.
    /// </summary>
    internal bool IsSignalDownTrending
        => TrendDirection == FuturesTrendType.DownTrending
           && (_macdSignal!.MACD == FuturesTrendDirectionType.DownTrending || _macdSignal.MACD == FuturesTrendDirectionType.TrendReversal);
}
