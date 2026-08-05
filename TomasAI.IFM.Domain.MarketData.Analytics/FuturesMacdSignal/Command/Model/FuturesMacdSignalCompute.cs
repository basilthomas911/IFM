using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

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
    readonly int _signalPeriod;

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
        ComputeMacdComponents(previousMacdSignals);
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

    void ComputeMacdComponents(IReadOnlyCollection<FuturesMacdSignalReadModel> signals)
    {
        if (signals.Count == 0)
            return;

        const double fastMultiplier = 2.0 / (FastPeriod + 1);
        const double slowMultiplier = 2.0 / (SlowPeriod + 1);
        var signalMultiplier = 2.0 / (_signalPeriod + 1);
        var initialized = false;
        var fastEma = 0d;
        var slowEma = 0d;
        var signalLine = 0d;

        foreach (var signal in signals)
        {
            var price = (double)signal.FuturesPrice;
            if (!initialized)
            {
                fastEma = price;
                slowEma = price;
                initialized = true;
                continue;
            }

            fastEma = (price - fastEma) * fastMultiplier + fastEma;
            slowEma = (price - slowEma) * slowMultiplier + slowEma;
            var macd = fastEma - slowEma;
            signalLine = (macd - signalLine) * signalMultiplier + signalLine;
        }

        MacdLine = fastEma - slowEma;
        SignalLine = signalLine;
        Histogram = MacdLine - SignalLine;
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
