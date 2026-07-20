using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesAtrSignal.Command.Model;

/// <summary>
/// Represents a model for analyzing futures trading signals based on ATR (Average True Range)
/// computed from RSI (Relative Strength Index) signal price values.
/// </summary>
/// <remarks>This model processes an array of FuturesRsiSignalReadModel instances, computing the True Range
/// and Average True Range to determine trend volatility direction and strength.</remarks>
public class FuturesAtrSignalCompute
{
    readonly FuturesAtrSignalReadModel? _atrSignal;
    readonly int _atrPeriod;

    public static bool Create(int atrPeriod, FuturesAtrSignalReadModel? atrSignal, IReadOnlyCollection<FuturesAtrSignalReadModel> atrSignals, out FuturesAtrSignalCompute model)
    {
        model = new(atrPeriod, atrSignals, atrSignal);
        return true;
    }

    FuturesAtrSignalCompute(int atrPeriod, IReadOnlyCollection<FuturesAtrSignalReadModel> atrSignals, FuturesAtrSignalReadModel? atrSignal = default)
    {
        _atrSignal = atrSignal;
        _atrPeriod = atrPeriod;

        // make sure signals are in ascending order...
        double[] priceValues = [.. atrSignals.Select(e => (double)e.FuturesPrice)];

        // compute ATR components from price values...
        ComputeAtrComponents(priceValues);
    }

    void ComputeAtrComponents(double[] priceValues)
    {
        var trueRanges = ComputeTrueRanges(priceValues);
        TrueRange = trueRanges.Length > 0 ? trueRanges[^1] : 0;
        AtrValue = ComputeAtr(trueRanges, _atrPeriod);

        /// <summary>   
        /// Computes the True Range values from an array of price values.
        /// </summary>
        /// <param name="prices">An array of price values.</param>
        /// <returns>An array of True Range values.</returns>
        static double[] ComputeTrueRanges(double[] prices)
        {
            if (prices.Length < 2) return [];
            var result = new double[prices.Length - 1];
            for (int i = 1; i < prices.Length; i++)
            {
                var highLow = Math.Abs(prices[i] - prices[i - 1]);
                result[i - 1] = highLow;
            }
            return result;
        }

        /// <summary>
        /// Computes the Average True Range (ATR) from an array of True Range values.
        /// </summary>
        /// <param name="trueRanges">An array of True Range values.</param>
        /// <param name="period">The period over which to compute the ATR.</param>
        /// <returns>The computed ATR value.</returns>
        static double ComputeAtr(double[] trueRanges, int period)
        {
            if (trueRanges.Length == 0) return 0;
            if (trueRanges.Length <= period)
            {
                return trueRanges.Average();
            }

            // initial ATR is SMA of first 'period' true ranges
            var atr = trueRanges.Take(period).Average();
            // smoothed ATR using Wilder's method
            for (int i = period; i < trueRanges.Length; i++)
            {
                atr = ((atr * (period - 1)) + trueRanges[i]) / period;
            }
            return atr;
        }
    }

    /// <summary>Average True Range value.</summary>
    public double AtrValue { get; private set; }

    /// <summary>True Range value for the current period.</summary>
    public double TrueRange { get; private set; }

    public FuturesTrendType TrendDirection
        => default(FuturesTrendType) switch
        {
            _ when TrueRange > AtrValue => FuturesTrendType.UpTrending,
            _ when TrueRange < AtrValue => FuturesTrendType.DownTrending,
            _ => FuturesTrendType.RangeBound
        };

    public FuturesTrendDirectionStrengthType TrendDirectionStrength()
    {
        if (AtrValue == 0) return FuturesTrendDirectionStrengthType.Low;
        var ratio = TrueRange / AtrValue;
        return ratio switch
        {
            >= 1.5 => FuturesTrendDirectionStrengthType.High,
            >= 1.0 => FuturesTrendDirectionStrengthType.Medium,
            _ => FuturesTrendDirectionStrengthType.Low
        };
    }

    /// <summary>
    /// Indicates whether no prior ATR signal exists (initial state).
    /// </summary>
    internal bool IsSignalInitializing
        => _atrSignal is null;

    /// <summary>
    /// Indicates whether the current ATR signal is in an up-trending state.
    /// </summary>
    internal bool IsSignalUpTrending
        => TrendDirection == FuturesTrendType.UpTrending
           && (_atrSignal!.ATR == FuturesTrendDirectionType.UpTrending || _atrSignal.ATR == FuturesTrendDirectionType.TrendReversal);

    /// <summary>
    /// Indicates whether the current ATR signal is in a down-trending state.
    /// </summary>
    internal bool IsSignalDownTrending
        => TrendDirection == FuturesTrendType.DownTrending
           && (_atrSignal!.ATR == FuturesTrendDirectionType.DownTrending || _atrSignal.ATR == FuturesTrendDirectionType.TrendReversal);

}
