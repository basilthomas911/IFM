using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAdxSignal.Command.Model;

/// <summary>
/// Represents a model for analyzing futures trading signals based on ADX (Average Directional Index)
/// computed from ITI (Intrinsic Time Indicator) signal price values.
/// </summary>
/// <remarks>This model processes an array of FuturesItiSignalV2ReadModel instances, computing the +DI, -DI,
/// and ADX values to determine trend direction and strength.</remarks>
public class FuturesAdxSignalCompute
{
    readonly FuturesAdxSignalReadModel? _adxSignal;
    readonly int _adxPeriod;

    public static bool Create(int adxPeriod, FuturesAdxSignalReadModel? adxSignal, 
        IReadOnlyCollection<FuturesAdxSignalReadModel> adxSignals, 
        out FuturesAdxSignalCompute model)
    {
        model = new(adxPeriod, adxSignals, adxSignal);
        return true;
    }

    FuturesAdxSignalCompute(int adxPeriod, IReadOnlyCollection<FuturesAdxSignalReadModel> adxSignals, FuturesAdxSignalReadModel? adxSignal = default)
    {
        _adxPeriod = adxPeriod;
        _adxSignal = adxSignal;

        // make sure signals are in ascending order...
        double[] priceValues = [.. adxSignals.Select(e => (double)e.FuturesPrice)];

        // compute ADX components from price values...
        ComputeAdxComponents(priceValues);
    }

    void ComputeAdxComponents(double[] priceValues)
    {
        var (plusDI, minusDI, adx) = ComputeAdx(priceValues, _adxPeriod);
        PlusDI = plusDI;
        MinusDI = minusDI;
        AdxValue = adx;
    }

    /// <summary>
    /// Computes the ADX, +DI, and -DI from an array of price values.
    /// </summary>
    /// <param name="prices">An array of price values in ascending time order.</param>
    /// <param name="period">The smoothing period for ADX calculation.</param>
    /// <returns>A tuple of (+DI, -DI, ADX) values.</returns>
    static (double PlusDI, double MinusDI, double Adx) ComputeAdx(double[] prices, int period)
    {
        if (prices.Length < 2) return (0, 0, 0);

        int n = prices.Length;
        var plusDm = new double[n - 1];
        var minusDm = new double[n - 1];
        var tr = new double[n - 1];

        for (int i = 1; i < n; i++)
        {
            var upMove = prices[i] - prices[i - 1];
            var downMove = prices[i - 1] - prices[i];
            plusDm[i - 1] = upMove > 0 && upMove > downMove ? upMove : 0;
            minusDm[i - 1] = downMove > 0 && downMove > upMove ? downMove : 0;
            tr[i - 1] = Math.Abs(prices[i] - prices[i - 1]);
        }

        if (tr.Length == 0) return (0, 0, 0);

        // Wilder's smoothing
        var smoothedTr = WilderSmooth(tr, period);
        var smoothedPlusDm = WilderSmooth(plusDm, period);
        var smoothedMinusDm = WilderSmooth(minusDm, period);

        if (smoothedTr == 0) return (0, 0, 0);

        var currentPlusDI = (smoothedPlusDm / smoothedTr) * 100;
        var currentMinusDI = (smoothedMinusDm / smoothedTr) * 100;

        var diSum = currentPlusDI + currentMinusDI;
        var dx = diSum == 0 ? 0 : (Math.Abs(currentPlusDI - currentMinusDI) / diSum) * 100;

        // ADX is the smoothed DX; for a single pass we use the DX value directly
        return (currentPlusDI, currentMinusDI, dx);
    }

    /// <summary>
    /// Applies Wilder's smoothing method to a data series and returns the final smoothed value.
    /// </summary>
    static double WilderSmooth(double[] data, int period)
    {
        if (data.Length == 0) return 0;
        if (data.Length <= period)
            return data.Average();

        // initial value is SMA of first 'period' values
        var smoothed = data.Take(period).Average();
        // apply Wilder's smoothing for remaining values
        for (int i = period; i < data.Length; i++)
        {
            smoothed = ((smoothed * (period - 1)) + data[i]) / period;
        }
        return smoothed;
    }

    /// <summary>Plus Directional Indicator (+DI) value.</summary>
    public double PlusDI { get; private set; }

    /// <summary>Minus Directional Indicator (-DI) value.</summary>
    public double MinusDI { get; private set; }

    /// <summary>Average Directional Index value.</summary>
    public double AdxValue { get; private set; }

    public FuturesTrendType TrendDirection
        => default(FuturesTrendType) switch
        {
            _ when PlusDI > MinusDI => FuturesTrendType.UpTrending,
            _ when MinusDI > PlusDI => FuturesTrendType.DownTrending,
            _ => FuturesTrendType.RangeBound
        };

    public FuturesTrendDirectionStrengthType TrendDirectionStrength()
    {
        return AdxValue switch
        {
            >= 50 => FuturesTrendDirectionStrengthType.High,
            >= 25 => FuturesTrendDirectionStrengthType.Medium,
            _ => FuturesTrendDirectionStrengthType.Low
        };
    }

    /// <summary>
    /// Indicates whether no prior ADX signal exists (initial state).
    /// </summary>
    internal bool IsSignalInitializing
        => _adxSignal is null;

    /// <summary>
    /// Indicates whether the current ADX signal is in an up-trending state.
    /// </summary>
    internal bool IsSignalUpTrending
        => TrendDirection == FuturesTrendType.UpTrending
           && (_adxSignal!.ADX == FuturesTrendDirectionType.UpTrending || _adxSignal.ADX == FuturesTrendDirectionType.TrendReversal);

    /// <summary>
    /// Indicates whether the current ADX signal is in a down-trending state.
    /// </summary>
    internal bool IsSignalDownTrending
        => TrendDirection == FuturesTrendType.DownTrending
           && (_adxSignal!.ADX == FuturesTrendDirectionType.DownTrending || _adxSignal.ADX == FuturesTrendDirectionType.TrendReversal);
}
