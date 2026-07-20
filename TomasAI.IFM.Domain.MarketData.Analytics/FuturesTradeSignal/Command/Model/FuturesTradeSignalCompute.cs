using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSignal.Command.Model;

internal class FuturesTradeSignalCompute
{
    readonly UpdateFuturesTradeSignalCommand _updateCmd;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="updateCmd"></param>
    /// <param name="model"></param>
    /// <returns></returns>
    internal static bool Create(UpdateFuturesTradeSignalCommand updateCmd, out FuturesTradeSignalCompute model)
    {
        model = new(updateCmd);
        return true;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="updateCmd"></param>
    FuturesTradeSignalCompute(UpdateFuturesTradeSignalCommand updateCmd)
    {
        _updateCmd = IsArgumentNull.Set(updateCmd);
        FuturesTradeSignal = ComputeFuturesTradeSignalFromEodData();
    }

    /// <summary>
    /// Gets the current futures trade signal represented by the FuturesTradeSignalV2ReadModel.
    /// </summary>
    /// <remarks>This property provides access to the latest trade signal for futures, which can be used to
    /// inform trading decisions. It is important to note that the value may change based on market
    /// conditions.</remarks>
    internal FuturesTradeSignalV2ReadModel FuturesTradeSignal { get; }

    /// <summary>
    /// Computes a trading signal for a futures contract using end-of-day market data and multiple technical indicators.
    /// </summary>
    /// <remarks>This method aggregates end-of-day data and technical signals such as RSI, TDI, and ITI to
    /// generate a comprehensive trading signal. Ensure that all required input data is current and valid to obtain
    /// accurate results.</remarks>
    /// <returns>A <see cref="FuturesTradeSignalV2ReadModel"/> containing the computed trading signal details, including risk
    /// metrics, trend analysis, and indicator values for the specified contract and date.</returns>
    FuturesTradeSignalV2ReadModel ComputeFuturesTradeSignalFromEodData()
    {
        var score = new FuturesTradeSignalModel(_updateCmd.FuturesEodData, _updateCmd.FuturesRsiSignal!, _updateCmd.FuturesTdiSignal!, _updateCmd.FuturesItiSignalData!);
        return new FuturesTradeSignalV2ReadModel(
            _updateCmd.FuturesEodData.ContractId,
            _updateCmd.FuturesEodData.ValueDate,
            TradeTimePeriodType.FifteenSeconds,
            0,
            TimeOnly.FromDateTime(DateTime.Now),
            _updateCmd.FuturesEodData.Mean,
            _updateCmd.FuturesEodData.DailyStdDev,
            Convert.ToDouble(_updateCmd.FuturesEodData.ClosePrice),
            _updateCmd.FuturesEodData.DailyPercentChange,
            score.FundRiskPercent,
            score.RSIAverage,
            score.RSISlope,
            score.FuturesTrendType,
            score.FuturesTrendStrength,
            score.TradeSignal,
            score.FuturesTrendDirection,
            score.TDIStrength,
            _updateCmd.FuturesEodData.MarketDirectionIndicator,
            score.MDITrend,
            score.MDIUpTrendLimit,
            score.MDIDownTrendLimit,
            score.UpTrendingTrigger,
            score.DownTrendingTrigger,
            score.EntryTrigger,
            score.ExitTrigger,
            score.TrendDelta,
            score.TrendExtreme,
            score.TrendReversal,
            score.FiftyDMA,
            score.TwoHundredDMA,
            score.TradeExecuteState
        );
    }
}
