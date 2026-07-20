using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Model;

/// <summary>
/// Represents a model that aggregates and interprets multiple market indicators to generate trading signals and trend
/// information for futures trading strategies.
/// </summary>
/// <remarks>This model combines data from various technical indicators, such as RSI, TDI, and intrinsic time
/// signals, to provide a unified view of market trends, risk levels, and actionable trade signals. It exposes
/// properties for trend direction, trend strength, risk assessment, and entry/exit triggers, enabling consumers to make
/// informed trading decisions based on current market conditions. The model is intended for use in algorithmic or
/// discretionary trading systems that require a synthesized analysis of futures market data.</remarks>
public class FuturesTradeSignalModel(
    FuturesEodDataV2ReadModel futuresEodData,
    FuturesRsiSignalReadModel rsiSignal,
    FuturesTdiSignalReadModel tdiSignal,
    FuturesItiSignalDataReadModel itiSignalData)
{
    readonly FuturesEodDataV2ReadModel _futuresEodData = futuresEodData;
    readonly FuturesRsiSignalReadModel _rsiSignal = rsiSignal;
    readonly FuturesTdiSignalReadModel _tdiSignal = tdiSignal;
    readonly FuturesItiSignalDataReadModel _itiSignalData = itiSignalData;

    public double RSIAverage => _rsiSignal?.RSI ?? 0.0;
    public double RSISlope => _rsiSignal?.RSISlope ?? 0.0;
    public FuturesTrendDirectionStrengthType TDIStrength => _tdiSignal?.TDIStrength ?? FuturesTrendDirectionStrengthType.Low;

    public double FundRiskPercent => _futuresEodData.MarketDirectionIndicator switch
    {
        _ when _futuresEodData is null => 0.6,
        _ when _futuresEodData.MarketDirectionIndicator < 30 => 0.6,
        _ when _futuresEodData.MarketDirectionIndicator < 60 => 0.75,
        _ when _futuresEodData.MarketDirectionIndicator >= 60 => 0.9,
        _ => 0.6
    };

    public FuturesTrendType FuturesTrendType => default(FuturesTrendType) switch
    {
        _ when _itiSignalData?.TrendDirectionChange is null => FuturesTrendType.RangeBound,
        _ when _itiSignalData.TrendDirectionChange.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend => FuturesTrendType.UpTrending,
        _ when _itiSignalData.TrendDirectionChange.IntrinsicTimeTrend == IntrinsicTimeTrendType.DownTrend => FuturesTrendType.DownTrending,
        _ => FuturesTrendType.RangeBound
    };

    public FuturesTrendStrengthType FuturesTrendStrength => _futuresEodData.MarketDirectionIndicator switch
    {
        _ when _futuresEodData is null => FuturesTrendStrengthType.Low,
        _ when _futuresEodData.MarketDirectionIndicator < 30 => FuturesTrendStrengthType.Low,
        _ when _futuresEodData.MarketDirectionIndicator < 60 => FuturesTrendStrengthType.Medium,
        _ when _futuresEodData.MarketDirectionIndicator >= 60 => FuturesTrendStrengthType.High,
        _ => FuturesTrendStrengthType.Low
    };

    bool FuturesItiSignaIMDIsRangeBound()
    {
        var e = GetLastFuturesItiSignal();
        return e switch
        {
            _ => true
        };
    }

    FuturesMDITrendType FuturesItiSignaIMDITrend()
    {
        var e = GetLastFuturesItiSignal();
        return e switch
        {
            null => FuturesMDITrendType.RangeBound,
            _ => FuturesMDITrendType.RangeBound
        };
    }

    FuturesItiSignalV2ReadModel? GetLastFuturesItiSignal()
    {
        return GetFuturesItiSignals()
            .OrderByDescending(e => e.IntrinsicTime)
            .FirstOrDefault();

        IEnumerable<FuturesItiSignalV2ReadModel> GetFuturesItiSignals()
        {
            if (_itiSignalData is null)
                yield break;
            if (_itiSignalData.TrendDirectionChange is not null)
                yield return _itiSignalData.TrendDirectionChange;
            if (_itiSignalData.TrendExtremeChange is not null)
                yield return _itiSignalData.TrendExtremeChange;
            if (_itiSignalData.TrendReversalChange is not null)
                yield return _itiSignalData.TrendReversalChange;
        }
    }

    public FuturesMDITrendType MDITrend => FuturesItiSignaIMDITrend();

    FuturesMDITrendType GetMDITrend()
        => GetLastFuturesItiSignal() switch
        {
            null => FuturesMDITrendType.RangeBound,
            FuturesItiSignalV2ReadModel => FuturesItiSignaIMDITrend()
        };

    public double MDIUpTrendLimit => GetMDIUpTrendLimit();
    double GetMDIUpTrendLimit()
        => GetLastFuturesItiSignal() switch
        {
            null => 0,
            _ => 0
        };

    public double MDIDownTrendLimit => GetMDIDownTrendLimit();
    double GetMDIDownTrendLimit()
        => GetLastFuturesItiSignal() switch
        {
            null => 0,
            _ => 0
        };

    public double UpTrendingTrigger => GetUpTrendingTrigger();
    double GetUpTrendingTrigger()
    {
        if (_itiSignalData is null || (_itiSignalData.TrendExtremeChange is null && _itiSignalData.TrendDirectionChange is null))
            return 0.0;
        return (_itiSignalData.TrendExtremeChange ?? _itiSignalData.TrendDirectionChange)!.UpTrendTrigger;
    }

    public double DownTrendingTrigger => GetDownTrendingTrigger();
    double GetDownTrendingTrigger()
    {
        if (_itiSignalData is null || (_itiSignalData.TrendExtremeChange is null && _itiSignalData.TrendDirectionChange is null))
            return 0.0;
        return (_itiSignalData.TrendExtremeChange ?? _itiSignalData.TrendDirectionChange)!.DownTrendTrigger;
    }

    public double EntryTrigger => _itiSignalData?.TrendDirectionChange?.IntrinsicPrice ?? 0.0;

    public double ExitTrigger => GetExitTrigger();
    double GetExitTrigger()
    {
        if (_itiSignalData is null || _itiSignalData.TrendDirectionChange is null)
            return 0.0;
        return _itiSignalData.TrendDirectionChange.IntrinsicTimeTrend switch
        {
            IntrinsicTimeTrendType.UpTrend => (1 + 2 * _itiSignalData.TrendDirectionChange.Lambda) * _itiSignalData.TrendDirectionChange.UpTrendTrigger,
            IntrinsicTimeTrendType.DownTrend => (1 - 2 * _itiSignalData.TrendDirectionChange.Lambda) * _itiSignalData.TrendDirectionChange.DownTrendTrigger,
            _ => 0.0
        };
    }

    public double TrendDelta => GetTrendDelta();
    double GetTrendDelta()
    {
        var futuresitiSignal = GetLastFuturesItiSignal();
        return futuresitiSignal is null
            ? 0
            : futuresitiSignal.TrendExtreme - futuresitiSignal.TrendPrice;
    }

    public double TrendExtreme => GetTrendExtreme();
    double GetTrendExtreme()
    {
        if (_itiSignalData is not null && _itiSignalData.TrendDirectionChange is not null)
            return _itiSignalData.TrendDirectionChange.TrendExtreme;
        return 0;
    }

    public double TrendReversal => GetTrendReversal();
    double GetTrendReversal()
    {
        if (_itiSignalData is not null && _itiSignalData.TrendDirectionChange is not null)
            return _itiSignalData.TrendDirectionChange.TrendReversal;
        return 0;
    }

    public TradeSignalType TradeSignal => FuturesTrendType switch
    {
        FuturesTrendType.UpTrend => TradeSignalType.Sell,
        FuturesTrendType.UpTrending => TradeSignalType.Sell,
        FuturesTrendType.DownTrend => TradeSignalType.Buy,
        FuturesTrendType.DownTrending => TradeSignalType.Buy,
        _ => TradeSignalType.None
    };

    public FuturesTrendDirectionType FuturesTrendDirection => _tdiSignal?.TDI ?? FuturesTrendDirectionType.Init;

    public decimal FiftyDMA => _futuresEodData.FiftyDMA;
    public decimal TwoHundredDMA => _futuresEodData.TwoHundredDMA;

    public TradeExecuteState TradeExecuteState => GetTradeExecuteState();
    TradeExecuteState GetTradeExecuteState()
    {
        var tradeExecuteState = TradeExecuteState.No;
        if (_itiSignalData is not null && _itiSignalData.TrendDirectionChange is not null && _itiSignalData.TrendExtremeChange is not null && _futuresEodData is not null && _tdiSignal is not null)
        {
            var entryLimitDelta = _itiSignalData.TrendExtremeChange.TargetDelta * (1 / Math.PI);
            var maxExtreme = Math.Abs(_itiSignalData.TrendExtremeChange.TrendExtreme - _itiSignalData.TrendDirectionChange.TrendPrice);
            tradeExecuteState = default(TradeExecuteState) switch
            {
                _ when CheckTradeExecuteState() => TradeExecuteState.No,
                _ when InHoldTradeExecuteState() => TradeExecuteState.Hold,

                _ when IsBelowUpTrendEntryLimit(entryLimitDelta, maxExtreme) => TradeExecuteState.ExitOnEntryLimit,
                _ when IsAboveDownTrendEntryLimit(entryLimitDelta, maxExtreme) => TradeExecuteState.ExitOnEntryLimit,

                _ when InUpTrendReversion(maxExtreme) => TradeExecuteState.ExitOnTrendReversion,
                _ when InDownTrendReversion(maxExtreme) => TradeExecuteState.ExitOnTrendReversion,

                _ when IsRangeBound() => TradeExecuteState.RangeBound,

                _ when CanEnterUpTrendingTrade() => TradeExecuteState.Enter,
                _ when CanEnterDownTrendingTrade() => TradeExecuteState.Enter,
                _ => tradeExecuteState
            };
        }
        return tradeExecuteState;

        bool CheckTradeExecuteState()
            => (DateTime.Now - _itiSignalData.TrendDirectionChange.IntrinsicTime).TotalSeconds <= 30;

        bool InHoldTradeExecuteState()
            => _itiSignalData.TrendDirectionChange.TradeState == IntrinsicTimeTradeState.Hold && (DateTime.Now - _itiSignalData.TrendDirectionChange.IntrinsicTime).TotalMinutes < 5;

        bool IsBelowUpTrendEntryLimit(double entryLimitDelta, double maxExtreme)
            => FuturesTrendType == FuturesTrendType.UpTrending
                && Convert.ToDouble(_futuresEodData.ClosePrice) < (maxExtreme > entryLimitDelta
                    ? _itiSignalData.TrendDirectionChange.IntrinsicPrice
                    : _itiSignalData.TrendDirectionChange.IntrinsicPrice + (maxExtreme - entryLimitDelta));

        bool IsAboveDownTrendEntryLimit(double entryLimitDelta, double maxExtreme)
            => FuturesTrendType == FuturesTrendType.DownTrending
                && Convert.ToDouble(_futuresEodData.ClosePrice) > (maxExtreme > entryLimitDelta
                    ? _itiSignalData.TrendDirectionChange.IntrinsicPrice
                    : _itiSignalData.TrendDirectionChange.IntrinsicPrice + (entryLimitDelta - maxExtreme));

        bool InUpTrendReversion(double maxExtreme)
            => FuturesTrendType == FuturesTrendType.UpTrending
                && _tdiSignal.TDI == FuturesTrendDirectionType.DownTrending
                && maxExtreme > _itiSignalData.TrendDirectionChange.TargetDelta;

        bool InDownTrendReversion(double maxExtreme)
            => FuturesTrendType == FuturesTrendType.DownTrending
                && _tdiSignal.TDI == FuturesTrendDirectionType.UpTrending
                && maxExtreme > _itiSignalData.TrendDirectionChange.TargetDelta;

        bool IsRangeBound()
            => FuturesItiSignaIMDIsRangeBound();

        bool CanEnterUpTrendingTrade()
            => FuturesTrendType == FuturesTrendType.UpTrending
                && Convert.ToDouble(_futuresEodData.ClosePrice) > _itiSignalData.TrendDirectionChange.IntrinsicPrice;

        bool CanEnterDownTrendingTrade()
            => FuturesTrendType == FuturesTrendType.DownTrending
                && _tdiSignal.TDI == FuturesTrendDirectionType.DownTrending
                && Convert.ToDouble(_futuresEodData.ClosePrice) < _itiSignalData.TrendDirectionChange.IntrinsicPrice;
    }
}
