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

    public static bool Create(
        decimal futuresPrice,
        IReadOnlyCollection<FuturesMacdSignalReadModel> previousMacdSignals,
        FuturesMacdConfiguration configuration,
        out FuturesMacdSignalCompute model)
    {
        model = new(futuresPrice, previousMacdSignals, configuration);
        return true;
    }

    FuturesMacdSignalCompute(
        decimal futuresPrice,
        IReadOnlyCollection<FuturesMacdSignalReadModel> previousMacdSignals,
        FuturesMacdConfiguration configuration)
    {
        _macdSignal = previousMacdSignals.LastOrDefault();
        ComputeMacdComponents((double)futuresPrice, configuration);
    }

    /// <summary>MACD line value (fast EMA minus slow EMA of RSI).</summary>
    public double MacdLine { get; private set; }

    /// <summary>Signal line value (EMA of MACD line series).</summary>
    public double SignalLine { get; private set; }

    /// <summary>Histogram value (MACD line minus signal line).</summary>
    public double Histogram { get; private set; }

    public double FastEma { get; private set; }

    public double SlowEma { get; private set; }

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

    void ComputeMacdComponents(double price, FuturesMacdConfiguration configuration)
    {
        if (_macdSignal is null)
        {
            FastEma = price;
            SlowEma = price;
            MacdLine = 0d;
            SignalLine = 0d;
            Histogram = 0d;
            return;
        }

        var previousFastEma = _macdSignal.FastEma == 0d
            ? (double)_macdSignal.FuturesPrice
            : _macdSignal.FastEma;
        var previousSlowEma = _macdSignal.SlowEma == 0d
            ? (double)_macdSignal.FuturesPrice
            : _macdSignal.SlowEma;
        var fastMultiplier = 2.0 / (configuration.FastEmaPeriod + 1);
        var slowMultiplier = 2.0 / (configuration.SlowEmaPeriod + 1);
        var signalMultiplier = 2.0 / (configuration.SignalEmaPeriod + 1);

        FastEma = previousFastEma + fastMultiplier * (price - previousFastEma);
        SlowEma = previousSlowEma + slowMultiplier * (price - previousSlowEma);
        MacdLine = FastEma - SlowEma;
        SignalLine = _macdSignal.SignalLine
            + signalMultiplier * (MacdLine - _macdSignal.SignalLine);
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
