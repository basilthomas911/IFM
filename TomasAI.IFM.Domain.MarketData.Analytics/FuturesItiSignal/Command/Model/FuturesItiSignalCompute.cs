using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Model;

/// <summary>
/// Represents a computed model for evaluating ITI signal state transitions.
/// </summary>
/// <remarks>
/// This compute model encapsulates the domain logic for determining which signal condition applies
/// based on the incoming command and the current actor state, then provides the necessary data
/// for event generation.
/// </remarks>
public class FuturesItiSignalCompute
{
    readonly GenerateFuturesItiSignalCommand _command;
    readonly FuturesItiSignalCommandState _state;

    /// <summary>
    /// Calculates the lambda value based on the VIX futures price.
    /// </summary>
    /// <param name="vixFuturesPrice">The VIX futures price used to calculate the lambda value.</param>
    /// <param name="baselineVix">The baseline VIX value for normalization. Default is 15.7.</param>
    /// <param name="baseLambdaFactor">The base lambda factor. Default is 0.003.</param>
    /// <returns>The calculated lambda value.</returns>
    static double CalculateLambda(double vixFuturesPrice, double baselineVix = 15.7, double baseLambdaFactor = 0.003)
    {
        var normalizedVol = vixFuturesPrice / baselineVix;
        var volatilityFactor = normalizedVol > 1.0
            ? Math.Sqrt(normalizedVol)
            : normalizedVol;
        var minLambda = (2.0 / Math.PI) * baseLambdaFactor;
        return Math.Max(minLambda, baseLambdaFactor * volatilityFactor);
    }

    /// <summary>
    /// Calculates the minimum threshold value for a trading signal based on intrinsic price, trend conditions, and
    /// trading parameters.
    /// </summary>
    /// <remarks>If the intrinsic time trend is a downtrend, the method always returns the minimum target delta.
    /// Otherwise, the threshold may be increased based on the relationship between the trend extreme, intrinsic price,
    /// and calculated deltas.</remarks>
    /// <param name="signal">A view model containing the intrinsic price, trend information, and related market data used in the threshold
    /// calculation. Cannot be null.</param>
    /// <param name="trendDelta">The current trend delta value, which may be used as the threshold if certain trend conditions are met.</param>
    /// <param name="tradingDays">The number of trading days considered in the calculation, used to adjust the threshold for market volatility.
    /// Must be non-negative.</param>
    /// <param name="lambda">A scaling coefficient applied to the intrinsic price to determine the minimum target delta.</param>
    /// <param name="futuresPriceTick">The price tick size for futures contracts. Defaults to 0.25 if not specified.</param>
    /// <returns>The calculated threshold value, which is either the minimum target delta or the greater of the trend delta and
    /// the minimum target delta, depending on the trend conditions.</returns>
    static double CalculateThreshold(FuturesItiSignalV2ReadModel signal, double trendDelta, int tradingDays, double lambda, double futuresPriceTick = 0.25)
    {
        var minTargetDelta = (signal.IntrinsicPrice * lambda) + (Math.Sqrt(tradingDays) * (futuresPriceTick * tradingDays));
        if (signal.IntrinsicTimeTrend == IntrinsicTimeTrendType.DownTrend)
            return minTargetDelta;
        var minExtremeDelta = minTargetDelta * 1 / Math.PI;
        return (signal.TrendExtreme - signal.IntrinsicPrice) > minExtremeDelta && signal.IntrinsicPrice > (signal.TrendPrice + minTargetDelta)
            ? Math.Max(trendDelta, minTargetDelta)
            : minTargetDelta;
    }

    static int DefaultTradingDays(TradeTimePeriodType timePeriod)
        => timePeriod switch
        {
            TradeTimePeriodType.Weekly => 5,
            TradeTimePeriodType.Monthly => 20,
            _ => throw new ArgumentOutOfRangeException(nameof(timePeriod), $"Unsupported time period: {timePeriod}")
        };

    public static bool Create(GenerateFuturesItiSignalCommand command, FuturesItiSignalCommandState state, out FuturesItiSignalCompute model)
    {
        model = new(command, state);
        return true;
    }

    FuturesItiSignalCompute(GenerateFuturesItiSignalCommand command, FuturesItiSignalCommandState state)
    {
        _command = command ?? throw new ArgumentNullException(nameof(command));
        _state = state ?? throw new ArgumentNullException(nameof(state));
    }

    // ─── Signal condition checks ────────────────────────────────────────

    internal bool IsStartOfDay
        => !_state.Exists(_command.EntityId);

    internal bool HasUpTrendDirectionChanged
        => _state.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend
            && _command.FuturesPrice < _state.DownTrendTrigger;

    internal bool HasUpTrendExtremeChanged
        => _state.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend
            && _command.FuturesPrice > _state.TrendExtreme;

    internal bool HasUpTrendReversalChanged
        => _state.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend
            && _command.FuturesPrice < _state.TrendReversal;

    internal bool HasDownTrendDirectionChanged
        => _state.IntrinsicTimeTrend == IntrinsicTimeTrendType.DownTrend
            && _command.FuturesPrice > _state.UpTrendTrigger;

    internal bool HasDownTrendExtremeChanged
        => _state.IntrinsicTimeTrend == IntrinsicTimeTrendType.DownTrend
            && _command.FuturesPrice < _state.TrendExtreme;

    internal bool HasDownTrendReversalChanged
        => _state.IntrinsicTimeTrend == IntrinsicTimeTrendType.DownTrend
            && _command.FuturesPrice > _state.TrendReversal;

    // ─── Signal computation methods ─────────────────────────────────────

    internal FuturesItiSignalV2ReadModel ComputeStartOfDaySignal()
    {
        var signal = new FuturesItiSignalV2ReadModel(
            contractId: _command.ContractId,
            valueDate: _command.ValueDate,
            timePeriod: _command.TimePeriod,
            sequenceId: 0,
            intrinsicTime: _command.Timestamp,
            intrinsicTimeGroupId: 0,
            intrinsicTimeLength: 0,
            intrinsicPrice: _command.FuturesPrice,
            intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
            intrinsicTimeMode: IntrinsicTimeModeType.TrendDirectionChanged,
            trendPrice: _command.FuturesPrice,
            trendExtreme: _command.FuturesPrice,
            trendReversal: _command.FuturesPrice,
            trendDelta: 0,
            targetDelta: 0,
            lambda: 0,
            tradingDays: 0,
            threshold: 0,
            upTrendTrigger: _command.FuturesPrice,
            downTrendTrigger: 0,
            tradeState: IntrinsicTimeTradeState.Ready);

        return EnrichSignal(signal);
    }

    internal FuturesItiSignalV2ReadModel ComputeUpTrendDirectionChangedSignal()
    {
        var intrinsicTimeGroupId = _state.IntrinsicTimeGroupId + 1;
        var signal = new FuturesItiSignalV2ReadModel(
            contractId: _command.ContractId,
            valueDate: _command.ValueDate,
            timePeriod: _command.TimePeriod,
            sequenceId: 0,
            intrinsicTime: _command.Timestamp,
            intrinsicTimeGroupId: intrinsicTimeGroupId,
            intrinsicTimeLength: 0,
            intrinsicPrice: _command.FuturesPrice,
            intrinsicTimeTrend: IntrinsicTimeTrendType.DownTrend,
            intrinsicTimeMode: IntrinsicTimeModeType.TrendDirectionChanged,
            trendPrice: _command.FuturesPrice,
            trendExtreme: _command.FuturesPrice,
            trendReversal: _state.TrendReversal,
            trendDelta: 0,
            targetDelta: 0,
            lambda: 0,
            tradingDays: 0,
            threshold: 0,
            upTrendTrigger: _state.TrendExtreme,
            downTrendTrigger: _command.FuturesPrice,
            tradeState: _state.TradeState);

        return EnrichSignal(signal);
    }

    internal FuturesItiSignalV2ReadModel ComputeUpTrendExtremeChangedSignal()
    {
        var signal = new FuturesItiSignalV2ReadModel(
            contractId: _command.ContractId,
            valueDate: _command.ValueDate,
            timePeriod: _command.TimePeriod,
            sequenceId: 0,
            intrinsicTime: _command.Timestamp,
            intrinsicTimeGroupId: _state.IntrinsicTimeGroupId,
            intrinsicTimeLength: 0,
            intrinsicPrice: _command.FuturesPrice,
            intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
            intrinsicTimeMode: IntrinsicTimeModeType.TrendExtremeChanged,
            trendPrice: _state.TrendPrice,
            trendExtreme: _command.FuturesPrice,
            trendReversal: _command.FuturesPrice,
            trendDelta: 0,
            targetDelta: 0,
            lambda: 0,
            tradingDays: 0,
            threshold: 0,
            upTrendTrigger: _command.FuturesPrice,
            downTrendTrigger: _state.DownTrendTrigger,
            tradeState: _state.TradeState);

        return EnrichSignal(signal);
    }

    internal FuturesItiSignalV2ReadModel ComputeUpTrendReversalChangedSignal()
    {
        var signal = new FuturesItiSignalV2ReadModel(
            contractId: _command.ContractId,
            valueDate: _command.ValueDate,
            timePeriod: _command.TimePeriod,
            sequenceId: 0,
            intrinsicTime: _command.Timestamp,
            intrinsicTimeGroupId: _state.IntrinsicTimeGroupId,
            intrinsicTimeLength: 0,
            intrinsicPrice: _command.FuturesPrice,
            intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
            intrinsicTimeMode: IntrinsicTimeModeType.TrendReversalChanged,
            trendPrice: _state.TrendPrice,
            trendExtreme: _state.TrendExtreme,
            trendReversal: _command.FuturesPrice,
            trendDelta: 0,
            targetDelta: 0,
            lambda: 0,
            tradingDays: 0,
            threshold: 0,
            upTrendTrigger: _state.UpTrendTrigger,
            downTrendTrigger: _state.DownTrendTrigger,
            tradeState: _state.TradeState);

        return EnrichSignal(signal);
    }

    internal FuturesItiSignalV2ReadModel ComputeDownTrendDirectionChangedSignal()
    {
        var intrinsicTimeGroupId = _state.IntrinsicTimeGroupId + 1;
        var signal = new FuturesItiSignalV2ReadModel(
            contractId: _command.ContractId,
            valueDate: _command.ValueDate,
            timePeriod: _command.TimePeriod,
            sequenceId: 0,
            intrinsicTime: _command.Timestamp,
            intrinsicTimeGroupId: intrinsicTimeGroupId,
            intrinsicTimeLength: 0,
            intrinsicPrice: _command.FuturesPrice,
            intrinsicTimeTrend: IntrinsicTimeTrendType.UpTrend,
            intrinsicTimeMode: IntrinsicTimeModeType.TrendDirectionChanged,
            trendPrice: _command.FuturesPrice,
            trendExtreme: _command.FuturesPrice,
            trendReversal: _state.TrendReversal,
            trendDelta: 0,
            targetDelta: 0,
            lambda: 0,
            tradingDays: 0,
            threshold: 0,
            upTrendTrigger: _command.FuturesPrice,
            downTrendTrigger: _state.TrendExtreme,
            tradeState: IntrinsicTimeTradeState.Ready);

        return EnrichSignal(signal);
    }

    internal FuturesItiSignalV2ReadModel ComputeDownTrendExtremeChangedSignal()
    {
        var signal = new FuturesItiSignalV2ReadModel(
            contractId: _command.ContractId,
            valueDate: _command.ValueDate,
            timePeriod: _command.TimePeriod,
            sequenceId: 0,
            intrinsicTime: _command.Timestamp,
            intrinsicTimeGroupId: _state.IntrinsicTimeGroupId,
            intrinsicTimeLength: 0,
            intrinsicPrice: _command.FuturesPrice,
            intrinsicTimeTrend: IntrinsicTimeTrendType.DownTrend,
            intrinsicTimeMode: IntrinsicTimeModeType.TrendExtremeChanged,
            trendPrice: _state.TrendPrice,
            trendExtreme: _command.FuturesPrice,
            trendReversal: _command.FuturesPrice,
            trendDelta: 0,
            targetDelta: 0,
            lambda: 0,
            tradingDays: 0,
            threshold: 0,
            upTrendTrigger: _state.UpTrendTrigger,
            downTrendTrigger: _command.FuturesPrice,
            tradeState: _state.TradeState);

        return EnrichSignal(signal);
    }

    internal FuturesItiSignalV2ReadModel ComputeDownTrendReversalChangedSignal()
    {
        var signal = new FuturesItiSignalV2ReadModel(
            contractId: _command.ContractId,
            valueDate: _command.ValueDate,
            timePeriod: _command.TimePeriod,
            sequenceId: 0,
            intrinsicTime: _command.Timestamp,
            intrinsicTimeGroupId: _state.IntrinsicTimeGroupId,
            intrinsicTimeLength: 0,
            intrinsicPrice: _command.FuturesPrice,
            intrinsicTimeTrend: IntrinsicTimeTrendType.DownTrend,
            intrinsicTimeMode: IntrinsicTimeModeType.TrendReversalChanged,
            trendPrice: _state.TrendPrice,
            trendExtreme: _state.TrendExtreme,
            trendReversal: _command.FuturesPrice,
            trendDelta: 0,
            targetDelta: 0,
            lambda: 0,
            tradingDays: 0,
            threshold: 0,
            upTrendTrigger: _state.UpTrendTrigger,
            downTrendTrigger: _state.DownTrendTrigger,
            tradeState: _state.TradeState);

        return EnrichSignal(signal);
    }

    internal FuturesItiSignalV2ReadModel ComputeTrendingSignal()
    {
        var signal = new FuturesItiSignalV2ReadModel(
            contractId: _command.ContractId,
            valueDate: _command.ValueDate,
            timePeriod: _command.TimePeriod,
            sequenceId: 0,
            intrinsicTime: _command.Timestamp,
            intrinsicTimeGroupId: _state.IntrinsicTimeGroupId,
            intrinsicTimeLength: 0,
            intrinsicPrice: _command.FuturesPrice,
            intrinsicTimeTrend: _state.IntrinsicTimeTrend,
            intrinsicTimeMode: IntrinsicTimeModeType.Trending,
            trendPrice: _command.FuturesPrice,
            trendExtreme: _state.TrendExtreme,
            trendReversal: _state.TrendReversal,
            trendDelta: 0,
            targetDelta: 0,
            lambda: 0,
            tradingDays: 0,
            threshold: 0,
            upTrendTrigger: _state.UpTrendTrigger,
            downTrendTrigger: _state.DownTrendTrigger,
            tradeState: _state.TradeState == IntrinsicTimeTradeState.Closed ? IntrinsicTimeTradeState.Ready : _state.TradeState);

        return EnrichSignal(signal);
    }

    FuturesItiSignalV2ReadModel EnrichSignal(FuturesItiSignalV2ReadModel signal)
    {
        var trendDelta = _state.GetTrendDelta(signal);
        var tradingDays = signal.TradingDays == 0
            ? DefaultTradingDays(_command.TimePeriod)
            : _state.TradingDays;
        var lambda = CalculateLambda(_command.VixFuturesPrice);
        var threshold = CalculateThreshold(signal, trendDelta, tradingDays, lambda);

        var upTrendTrigger = signal.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend && signal.IntrinsicTimeMode == IntrinsicTimeModeType.TrendExtremeChanged
            ? _command.FuturesPrice
            : signal.IntrinsicTimeTrend == IntrinsicTimeTrendType.DownTrend && signal.IntrinsicTimeMode == IntrinsicTimeModeType.TrendExtremeChanged
            ? _command.FuturesPrice + threshold
            : signal.UpTrendTrigger;

        var downTrendTrigger = signal.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend && signal.IntrinsicTimeMode == IntrinsicTimeModeType.TrendExtremeChanged
            ? _command.FuturesPrice - threshold
            : signal.IntrinsicTimeTrend == IntrinsicTimeTrendType.DownTrend && signal.IntrinsicTimeMode == IntrinsicTimeModeType.TrendExtremeChanged
            ? _command.FuturesPrice
            : signal.DownTrendTrigger;

        return signal with
        {
            TrendDelta = trendDelta,
            TradingDays = tradingDays,
            Lambda = lambda,
            Threshold = threshold,
            UpTrendTrigger = upTrendTrigger,
            DownTrendTrigger = downTrendTrigger
        };
    }
}
