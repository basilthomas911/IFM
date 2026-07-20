using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.Events;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Model;

internal static class FuturesTdiSignalModel
{
   
}

/// <summary>
/// Represents a compute model for analyzing futures trading signals based on RSI (Relative Strength Index) values.
/// </summary>
/// <remarks>This model processes an array of FuturesRsiSignalReadModel instances, providing insights into trend
/// direction and strength based on recent RSI data. It calculates uptrend and downtrend counts over specified time
/// intervals, allowing users to assess market conditions effectively.</remarks>
internal class FuturesTdiSignalCompute
{
    readonly FuturesRsiSignalReadModel[] _futuresRsiSignals;
    readonly FuturesRsiSignalReadModel _currentRsiSignal;
    readonly FuturesTdiSignalReadModel? _tdiSignal;

    internal static bool Create(FuturesRsiSignalReadModel[] futuresRsiSignals, FuturesTdiSignalReadModel? tdiSignal, out FuturesTdiSignalCompute model)
    {
        model = new(futuresRsiSignals, tdiSignal);
        return true;
    }

    FuturesTdiSignalCompute(FuturesRsiSignalReadModel[] futuresRsiSignals, FuturesTdiSignalReadModel? tdiSignal = default)
    {
        _tdiSignal = tdiSignal;
        // make sure signals are in ascending order...
        _futuresRsiSignals = [.. futuresRsiSignals.OrderBy(e => e.Timestamp)];

        // get current signal..
        _currentRsiSignal = _futuresRsiSignals.Last();
    }

    /// <summary>
    /// get uptrend count for last 5 minutes...
    /// </summary>
    internal int UpTrendCount => _futuresRsiSignals
            .Where(e => e.Timestamp >= _currentRsiSignal.Timestamp.AddMinutes(-5) && e.Timestamp <= _currentRsiSignal.Timestamp)
            .Count(e => e.RSI >= 50);

    /// <summary>
    /// get downtrend count for last 10 minutes...
    /// </summary>
    internal int DownTrendCount => _futuresRsiSignals
            .Where(e => e.Timestamp >= _currentRsiSignal.Timestamp.AddMinutes(-5) && e.Timestamp <= _currentRsiSignal.Timestamp)
            .Count(e => e.RSI < 50);

    /// <summary>
    /// Gets the current trend direction of the futures market as determined by the Relative Strength Index (RSI) and
    /// its slope.
    /// </summary>
    /// <remarks>The trend direction is classified as uptrending if the RSI is 50 or higher and the RSI slope
    /// is non-negative, downtrending if the RSI is below 50 and the slope is negative, and range-bound otherwise. This
    /// property provides a high-level indication of market momentum based on recent RSI signals.</remarks>
    internal FuturesTrendType TrendDirection
        => default(FuturesTrendType) switch {
            _ when (_currentRsiSignal?.RSI ?? 0) >= 50 && (_currentRsiSignal?.RSISlope ?? -1) >= 0 => FuturesTrendType.UpTrending,
            _ when (_currentRsiSignal?.RSI ?? 51) < 50 && (_currentRsiSignal?.RSISlope ?? 0) < 0 => FuturesTrendType.DownTrending,
            _ => FuturesTrendType.RangeBound
        };

    /// <summary>
    /// Determines the strength of the current trend direction based on the number of consecutive upward or downward
    /// trends.
    /// </summary>
    /// <remarks>A trend is considered strong if there are two or more consecutive trends in the same
    /// direction. A single trend in the current direction is classified as medium strength. If there are no trends or
    /// the direction does not match, the strength is considered low.</remarks>
    /// <returns>A value of type FuturesTrendDirectionStrengthType that indicates the strength of the current trend. Returns High
    /// for strong trends, Medium for moderate trends, and Low for weak or absent trends.</returns>
    internal FuturesTrendDirectionStrengthType TrendDirectionStrength()
    {
        if (UpTrendCount > 0 && TrendDirection == FuturesTrendType.UpTrending)
        {
            return default(FuturesTrendDirectionStrengthType) switch
            {
                _ when UpTrendCount >= 2 => FuturesTrendDirectionStrengthType.High,
                _ when UpTrendCount == 1 => FuturesTrendDirectionStrengthType.Medium,
                _ => FuturesTrendDirectionStrengthType.Low
            };
        }
        else if (DownTrendCount > 0 && TrendDirection == FuturesTrendType.DownTrending)
        {
            return default(FuturesTrendDirectionStrengthType) switch
            {
                _ when DownTrendCount >= 2 => FuturesTrendDirectionStrengthType.High,
                _ when DownTrendCount == 1 => FuturesTrendDirectionStrengthType.Medium,
                _ => FuturesTrendDirectionStrengthType.Low
            };
        }
        return FuturesTrendDirectionStrengthType.Low;
    }

    /// <summary>
    /// Indicates whether no prior TDI signal exists (initial state).
    /// </summary>
    internal bool IsSignalInitializing
        => _tdiSignal is null;

    /// <summary>
    /// Indicates whether the current TDI signal is in an up-trending state.
    /// </summary>
    internal bool IsSignalUpTrending
        => !IsSignalInitializing
           && TrendDirection == FuturesTrendType.UpTrending
           && (_tdiSignal!.TDI == FuturesTrendDirectionType.UpTrending || _tdiSignal.TDI == FuturesTrendDirectionType.TrendReversal);

    /// <summary>
    /// Indicates whether the current TDI signal is in a down-trending state.
    /// </summary>
    internal bool IsSignalDownTrending
        => !IsSignalInitializing
           && TrendDirection == FuturesTrendType.DownTrending
           && (_tdiSignal!.TDI == FuturesTrendDirectionType.DownTrending || _tdiSignal.TDI == FuturesTrendDirectionType.TrendReversal);

    /// <summary>
    /// Indicates whether a trend reversal has occurred.
    /// A trend reversal happens when:
    /// - Prior was UpTrending and current is DownTrending or RangeBound
    /// - Prior was DownTrending and current is UpTrending or RangeBound  
    /// - Prior was Init and current is UpTrending or DownTrending
    /// - Prior was Flat and current is UpTrending or DownTrending
    /// </summary>
    internal bool IsSignalTrendReversal
        => !IsSignalInitializing
           && (
               // Prior UpTrending → now DownTrending or RangeBound
               (_tdiSignal!.TDI == FuturesTrendDirectionType.UpTrending && TrendDirection != FuturesTrendType.UpTrending)
               // Prior DownTrending → now UpTrending or RangeBound
               || (_tdiSignal.TDI == FuturesTrendDirectionType.DownTrending && TrendDirection != FuturesTrendType.DownTrending)
               // Prior Init → now any definite trend (UpTrending or DownTrending)
               || (_tdiSignal.TDI == FuturesTrendDirectionType.Init && TrendDirection != FuturesTrendType.RangeBound)
               // Prior Flat → now any definite trend (UpTrending or DownTrending)
               || (_tdiSignal.TDI == FuturesTrendDirectionType.Flat && TrendDirection != FuturesTrendType.RangeBound)
           );
}
