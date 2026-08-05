using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

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

        ComputeAtrComponents(atrSignals);
    }

    void ComputeAtrComponents(IReadOnlyCollection<FuturesAtrSignalReadModel> signals)
    {
        if (signals.Count < 2)
            return;

        var hasPrevious = false;
        var previousPrice = 0d;
        var rangeCount = 0;
        var initialSum = 0d;
        var atr = 0d;

        foreach (var signal in signals)
        {
            var price = (double)signal.FuturesPrice;
            if (!hasPrevious)
            {
                previousPrice = price;
                hasPrevious = true;
                continue;
            }

            var trueRange = Math.Abs(price - previousPrice);
            previousPrice = price;
            TrueRange = trueRange;
            rangeCount++;

            if (rangeCount <= _atrPeriod)
            {
                initialSum += trueRange;
                atr = initialSum / rangeCount;
            }
            else
            {
                atr = ((atr * (_atrPeriod - 1)) + trueRange) / _atrPeriod;
            }
        }

        AtrValue = atr;
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
