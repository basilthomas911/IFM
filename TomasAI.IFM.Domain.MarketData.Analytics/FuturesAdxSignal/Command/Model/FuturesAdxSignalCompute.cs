using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

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

        ComputeAdxComponents(adxSignals);
    }

    void ComputeAdxComponents(IReadOnlyCollection<FuturesAdxSignalReadModel> signals)
    {
        var (plusDI, minusDI, adx) = ComputeAdx(signals, _adxPeriod);
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
    static (double PlusDI, double MinusDI, double Adx) ComputeAdx(
        IReadOnlyCollection<FuturesAdxSignalReadModel> signals,
        int period)
    {
        if (signals.Count < 2)
            return (0, 0, 0);

        var hasPrevious = false;
        var previousPrice = 0d;
        var movementCount = 0;
        var trSum = 0d;
        var plusDmSum = 0d;
        var minusDmSum = 0d;
        var smoothedTr = 0d;
        var smoothedPlusDm = 0d;
        var smoothedMinusDm = 0d;

        foreach (var signal in signals)
        {
            var price = (double)signal.FuturesPrice;
            if (!hasPrevious)
            {
                previousPrice = price;
                hasPrevious = true;
                continue;
            }

            var upMove = price - previousPrice;
            var downMove = previousPrice - price;
            previousPrice = price;
            var plusDm = upMove > 0 && upMove > downMove ? upMove : 0;
            var minusDm = downMove > 0 && downMove > upMove ? downMove : 0;
            var trueRange = Math.Abs(upMove);
            movementCount++;

            if (movementCount <= period)
            {
                trSum += trueRange;
                plusDmSum += plusDm;
                minusDmSum += minusDm;
                smoothedTr = trSum / movementCount;
                smoothedPlusDm = plusDmSum / movementCount;
                smoothedMinusDm = minusDmSum / movementCount;
            }
            else
            {
                smoothedTr = ((smoothedTr * (period - 1)) + trueRange) / period;
                smoothedPlusDm = ((smoothedPlusDm * (period - 1)) + plusDm) / period;
                smoothedMinusDm = ((smoothedMinusDm * (period - 1)) + minusDm) / period;
            }
        }

        if (smoothedTr == 0) return (0, 0, 0);

        var currentPlusDI = (smoothedPlusDm / smoothedTr) * 100;
        var currentMinusDI = (smoothedMinusDm / smoothedTr) * 100;

        var diSum = currentPlusDI + currentMinusDI;
        var dx = diSum == 0 ? 0 : (Math.Abs(currentPlusDI - currentMinusDI) / diSum) * 100;

        // ADX is the smoothed DX; for a single pass we use the DX value directly
        return (currentPlusDI, currentMinusDI, dx);
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
