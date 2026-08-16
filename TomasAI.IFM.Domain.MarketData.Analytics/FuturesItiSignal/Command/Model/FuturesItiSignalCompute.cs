using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Model;

/// <summary>
/// Pure transition evaluator shared by the realtime pre-filter and durable ITI
/// command actor. Direction changes remain trigger-driven; all other recurring
/// publications require movement of ten percent of the calculated ITI threshold.
/// </summary>
public sealed class FuturesItiSignalCompute
{
    internal const double DefaultBandPercentage = 0.10;
    const double FuturesPriceTick = 0.25;

    readonly GenerateFuturesItiSignalCommand _command;
    readonly FuturesItiSignalV2ReadModel? _current;

    FuturesItiSignalCompute(
        GenerateFuturesItiSignalCommand command,
        FuturesItiSignalV2ReadModel? current)
    {
        _command = command ?? throw new ArgumentNullException(nameof(command));
        _current = current;
    }

    public static bool Create(
        GenerateFuturesItiSignalCommand command,
        FuturesItiSignalCommandState state,
        out FuturesItiSignalCompute model)
    {
        ArgumentNullException.ThrowIfNull(state);
        model = new FuturesItiSignalCompute(command, state.CurrentSignal);
        return true;
    }

    /// <summary>
    /// Evaluates a tick against an optional last durable signal. This overload is
    /// used by the realtime actor so ticks inside the active band remain hot-only.
    /// </summary>
    internal static bool TryCompute(
        GenerateFuturesItiSignalCommand command,
        FuturesItiSignalV2ReadModel? current,
        out FuturesItiSignalV2ReadModel signal)
        => new FuturesItiSignalCompute(command, current).TryCompute(out signal);

    internal bool TryCompute(out FuturesItiSignalV2ReadModel signal)
    {
        if (!IsCurrentFrame())
        {
            signal = Enrich(CreateStartOfTimeFrameSignal());
            return true;
        }

        var current = _current!;
        var price = _command.FuturesPrice;
        var bandSize = CurrentBandSize(current);

        if (current.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend)
        {
            if (price <= current.DownTrendTrigger)
                signal = Enrich(CreateDirectionChangedSignal(IntrinsicTimeTrendType.DownTrend));
            else if (price >= current.TrendExtreme + bandSize)
                signal = Enrich(CreateExtremeChangedSignal());
            else if (price <= current.TrendReversal - bandSize)
                signal = Enrich(CreateReversalChangedSignal());
            else if (HasMovedOneBand(current, price, bandSize))
                signal = Enrich(CreateTrendingSignal());
            else
            {
                signal = default!;
                return false;
            }
        }
        else
        {
            if (price >= current.UpTrendTrigger)
                signal = Enrich(CreateDirectionChangedSignal(IntrinsicTimeTrendType.UpTrend));
            else if (price <= current.TrendExtreme - bandSize)
                signal = Enrich(CreateExtremeChangedSignal());
            else if (price >= current.TrendReversal + bandSize)
                signal = Enrich(CreateReversalChangedSignal());
            else if (HasMovedOneBand(current, price, bandSize))
                signal = Enrich(CreateTrendingSignal());
            else
            {
                signal = default!;
                return false;
            }
        }

        return true;
    }

    bool IsCurrentFrame()
        => _current is not null
            && StringComparer.Ordinal.Equals(_current.ContractId, _command.ContractId)
            && _current.TimePeriod == _command.TimePeriod
            && EffectiveFrameStart(_current) == _command.TimeFrameStartValueDate;

    static DateOnly EffectiveFrameStart(FuturesItiSignalV2ReadModel signal)
        => signal.TimeFrameStartValueDate == default
            ? signal.ValueDate
            : signal.TimeFrameStartValueDate;

    static double CurrentBandSize(FuturesItiSignalV2ReadModel signal)
    {
        if (signal.BandSize > 0)
            return signal.BandSize;
        if (signal.Threshold > 0)
            return signal.Threshold * DefaultBandPercentage;
        return FuturesPriceTick * DefaultBandPercentage;
    }

    static bool HasMovedOneBand(
        FuturesItiSignalV2ReadModel current,
        double price,
        double bandSize)
    {
        var anchor = current.BandAnchorPrice == 0
            ? current.IntrinsicPrice
            : current.BandAnchorPrice;
        return Math.Abs(price - anchor) >= bandSize;
    }

    FuturesItiSignalV2ReadModel CreateStartOfTimeFrameSignal()
        => CreateBaseSignal(
            groupId: 0,
            trend: IntrinsicTimeTrendType.UpTrend,
            mode: IntrinsicTimeModeType.TrendDirectionChanged,
            trendPrice: _command.FuturesPrice,
            trendExtreme: _command.FuturesPrice,
            trendReversal: _command.FuturesPrice,
            tradeState: IntrinsicTimeTradeState.Ready);

    FuturesItiSignalV2ReadModel CreateDirectionChangedSignal(
        IntrinsicTimeTrendType nextTrend)
        => CreateBaseSignal(
            groupId: _current!.IntrinsicTimeGroupId + 1,
            trend: nextTrend,
            mode: IntrinsicTimeModeType.TrendDirectionChanged,
            trendPrice: _command.FuturesPrice,
            trendExtreme: _command.FuturesPrice,
            trendReversal: _command.FuturesPrice,
            tradeState: _current.TradeState);

    FuturesItiSignalV2ReadModel CreateExtremeChangedSignal()
        => CreateBaseSignal(
            groupId: _current!.IntrinsicTimeGroupId,
            trend: _current.IntrinsicTimeTrend,
            mode: IntrinsicTimeModeType.TrendExtremeChanged,
            trendPrice: _current.TrendPrice,
            trendExtreme: _command.FuturesPrice,
            trendReversal: _command.FuturesPrice,
            tradeState: _current.TradeState);

    FuturesItiSignalV2ReadModel CreateReversalChangedSignal()
        => CreateBaseSignal(
            groupId: _current!.IntrinsicTimeGroupId,
            trend: _current.IntrinsicTimeTrend,
            mode: IntrinsicTimeModeType.TrendReversalChanged,
            trendPrice: _current.TrendPrice,
            trendExtreme: _current.TrendExtreme,
            trendReversal: _command.FuturesPrice,
            tradeState: _current.TradeState);

    FuturesItiSignalV2ReadModel CreateTrendingSignal()
        => CreateBaseSignal(
            groupId: _current!.IntrinsicTimeGroupId,
            trend: _current.IntrinsicTimeTrend,
            mode: IntrinsicTimeModeType.Trending,
            trendPrice: _current.TrendPrice,
            trendExtreme: _current.TrendExtreme,
            trendReversal: _current.TrendReversal,
            tradeState: _current.TradeState == IntrinsicTimeTradeState.Closed
                ? IntrinsicTimeTradeState.Ready
                : _current.TradeState);

    FuturesItiSignalV2ReadModel CreateBaseSignal(
        int groupId,
        IntrinsicTimeTrendType trend,
        IntrinsicTimeModeType mode,
        double trendPrice,
        double trendExtreme,
        double trendReversal,
        IntrinsicTimeTradeState tradeState)
        => new(
            contractId: _command.ContractId,
            valueDate: _command.ValueDate,
            timePeriod: _command.TimePeriod,
            sequenceId: 0,
            intrinsicTime: _command.Timestamp,
            intrinsicTimeGroupId: groupId,
            intrinsicTimeLength: _current is null
                ? 0
                : Math.Max(0, (_command.Timestamp - _current.IntrinsicTime).TotalSeconds),
            intrinsicPrice: _command.FuturesPrice,
            intrinsicTimeTrend: trend,
            intrinsicTimeMode: mode,
            trendPrice: trendPrice,
            trendExtreme: trendExtreme,
            trendReversal: trendReversal,
            trendDelta: trendExtreme - _command.FuturesPrice,
            targetDelta: 0,
            lambda: 0,
            tradingDays: DefaultTradingDays(_command.TimePeriod),
            threshold: 0,
            upTrendTrigger: _current?.UpTrendTrigger ?? _command.FuturesPrice,
            downTrendTrigger: _current?.DownTrendTrigger ?? _command.FuturesPrice,
            tradeState: tradeState,
            timeFrameStartValueDate: _command.TimeFrameStartValueDate,
            bandAnchorPrice: _command.FuturesPrice,
            bandPercentage: DefaultBandPercentage,
            bandSize: 0);

    FuturesItiSignalV2ReadModel Enrich(FuturesItiSignalV2ReadModel signal)
    {
        var lambda = CalculateLambda(_command.VixFuturesPrice);
        var threshold = CalculateThreshold(signal, lambda);
        var upTrendTrigger = signal.UpTrendTrigger;
        var downTrendTrigger = signal.DownTrendTrigger;

        if (signal.IntrinsicTimeMode is IntrinsicTimeModeType.TrendDirectionChanged
            or IntrinsicTimeModeType.TrendExtremeChanged)
        {
            if (signal.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend)
            {
                upTrendTrigger = _command.FuturesPrice;
                downTrendTrigger = _command.FuturesPrice - threshold;
            }
            else
            {
                upTrendTrigger = _command.FuturesPrice + threshold;
                downTrendTrigger = _command.FuturesPrice;
            }
        }

        return signal with
        {
            Lambda = lambda,
            Threshold = threshold,
            BandAnchorPrice = _command.FuturesPrice,
            BandPercentage = DefaultBandPercentage,
            BandSize = threshold * DefaultBandPercentage,
            UpTrendTrigger = upTrendTrigger,
            DownTrendTrigger = downTrendTrigger
        };
    }

    static double CalculateLambda(
        double vixFuturesPrice,
        double baselineVix = 15.7,
        double baseLambdaFactor = 0.003)
    {
        var normalizedVolatility = vixFuturesPrice / baselineVix;
        var volatilityFactor = normalizedVolatility > 1
            ? Math.Sqrt(normalizedVolatility)
            : normalizedVolatility;
        var minimumLambda = (2.0 / Math.PI) * baseLambdaFactor;
        return Math.Max(minimumLambda, baseLambdaFactor * volatilityFactor);
    }

    static double CalculateThreshold(
        FuturesItiSignalV2ReadModel signal,
        double lambda)
    {
        var minimumTargetDelta = signal.IntrinsicPrice * lambda
            + Math.Sqrt(signal.TradingDays) * (FuturesPriceTick * signal.TradingDays);
        if (signal.IntrinsicTimeTrend == IntrinsicTimeTrendType.DownTrend)
            return minimumTargetDelta;

        var minimumExtremeDelta = minimumTargetDelta / Math.PI;
        return signal.TrendExtreme - signal.IntrinsicPrice > minimumExtremeDelta
            && signal.IntrinsicPrice > signal.TrendPrice + minimumTargetDelta
                ? Math.Max(signal.TrendDelta, minimumTargetDelta)
                : minimumTargetDelta;
    }

    static int DefaultTradingDays(TimeFrameType timePeriod)
        => timePeriod switch
        {
            TimeFrameType.Daily => 1,
            TimeFrameType.Weekly => 5,
            TimeFrameType.Monthly => 20,
            _ => throw new ArgumentOutOfRangeException(
                nameof(timePeriod),
                $"Unsupported ITI time period: {timePeriod}")
        };
}
