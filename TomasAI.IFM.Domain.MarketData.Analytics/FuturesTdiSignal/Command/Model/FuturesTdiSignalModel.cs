using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Model;

/// <summary>
/// Deterministically calculates the Traders Dynamic Index from a bounded series of RSI observations.
/// </summary>
internal sealed class FuturesTdiSignalCompute
{
    readonly FuturesTdiSignalReadModel? _previous;

    internal FuturesRsiSignalReadModel CurrentRsiSignal { get; }
    internal double PriceLine { get; }
    internal double SignalLine { get; }
    internal double MarketBaseLine { get; }
    internal double UpperVolatilityBand { get; }
    internal double LowerVolatilityBand { get; }
    internal FuturesTdiCrossType Cross { get; }
    internal FuturesTdiMarketStateType MarketState { get; }
    internal FuturesTrendDirectionType TrendDirection { get; }
    internal FuturesTrendDirectionStrengthType TrendStrength { get; }

    internal static bool Create(
        FuturesRsiSignalReadModel[] futuresRsiSignals,
        FuturesTdiSignalReadModel? previous,
        FuturesTdiConfiguration configuration,
        out FuturesTdiSignalCompute? model)
    {
        ArgumentNullException.ThrowIfNull(futuresRsiSignals);
        ArgumentNullException.ThrowIfNull(configuration);

        if (futuresRsiSignals.Length < configuration.RequiredRsiSamples)
        {
            model = null;
            return false;
        }

        model = new FuturesTdiSignalCompute(futuresRsiSignals, previous, configuration);
        return true;
    }

    FuturesTdiSignalCompute(
        FuturesRsiSignalReadModel[] futuresRsiSignals,
        FuturesTdiSignalReadModel? previous,
        FuturesTdiConfiguration configuration)
    {
        _previous = previous is { SchemaVersion: FuturesTdiConfiguration.CurrentSchemaVersion }
            ? previous
            : null;

        var ordered = IsAscending(futuresRsiSignals)
            ? futuresRsiSignals
            : [.. futuresRsiSignals.OrderBy(static x => x.ValueDate).ThenBy(static x => x.Timestamp)];

        CurrentRsiSignal = ordered[^1];
        PriceLine = AverageLast(ordered, configuration.PriceLinePeriod);
        SignalLine = AverageLast(ordered, configuration.SignalLinePeriod);
        MarketBaseLine = AverageLast(ordered, configuration.MarketBasePeriod);

        var bandValues = ordered.AsSpan(ordered.Length - configuration.VolatilityBandPeriod);
        var variance = 0d;
        foreach (var value in bandValues)
        {
            var difference = value.RSI - MarketBaseLine;
            variance += difference * difference;
        }

        var standardDeviation = Math.Sqrt(variance / bandValues.Length);
        var bandOffset = configuration.VolatilityBandDeviation * standardDeviation;
        UpperVolatilityBand = MarketBaseLine + bandOffset;
        LowerVolatilityBand = MarketBaseLine - bandOffset;

        var divergence = PriceLine - SignalLine;
        var previousDivergence = _previous?.PriceSignalDivergence;
        Cross = previousDivergence switch
        {
            <= 0d when divergence > 0d => FuturesTdiCrossType.Bullish,
            >= 0d when divergence < 0d => FuturesTdiCrossType.Bearish,
            _ => FuturesTdiCrossType.None
        };

        MarketState = PriceLine switch
        {
            var value when value <= configuration.OversoldLevel => FuturesTdiMarketStateType.Oversold,
            var value when value < configuration.Midline => FuturesTdiMarketStateType.BelowMidline,
            var value when value >= configuration.OverboughtLevel => FuturesTdiMarketStateType.Overbought,
            _ => FuturesTdiMarketStateType.AboveMidline
        };

        TrendDirection = Cross switch
        {
            FuturesTdiCrossType.Bullish or FuturesTdiCrossType.Bearish => FuturesTrendDirectionType.TrendReversal,
            _ when PriceLine > SignalLine && PriceLine >= MarketBaseLine => FuturesTrendDirectionType.UpTrending,
            _ when PriceLine < SignalLine && PriceLine <= MarketBaseLine => FuturesTrendDirectionType.DownTrending,
            _ => FuturesTrendDirectionType.Flat
        };

        TrendStrength = TrendDirection switch
        {
            FuturesTrendDirectionType.UpTrending when PriceLine >= UpperVolatilityBand => FuturesTrendDirectionStrengthType.High,
            FuturesTrendDirectionType.DownTrending when PriceLine <= LowerVolatilityBand => FuturesTrendDirectionStrengthType.High,
            FuturesTrendDirectionType.UpTrending or FuturesTrendDirectionType.DownTrending or FuturesTrendDirectionType.TrendReversal
                => FuturesTrendDirectionStrengthType.Medium,
            _ => FuturesTrendDirectionStrengthType.Low
        };
    }

    static double AverageLast(FuturesRsiSignalReadModel[] values, int period)
    {
        var span = values.AsSpan(values.Length - period);
        var sum = 0d;
        foreach (var value in span)
            sum += value.RSI;
        return sum / span.Length;
    }

    static bool IsAscending(FuturesRsiSignalReadModel[] signals)
    {
        for (var index = 1; index < signals.Length; index++)
        {
            if (signals[index - 1].ValueDate > signals[index].ValueDate
                || signals[index - 1].ValueDate == signals[index].ValueDate
                && signals[index - 1].Timestamp > signals[index].Timestamp)
                return false;
        }
        return true;
    }
}
