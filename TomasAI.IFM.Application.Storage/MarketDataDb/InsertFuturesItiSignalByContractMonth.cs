using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesItiSignalByContractMonth(int yearMonth, string contractId, DateOnly valueDate, string timePeriod, long sequenceId, DateTime intrinsicTime, int intrinsicTimeGroupId, double intrinsicTimeLength, double intrinsicPrice, string intrinsicTimeTrend, string intrinsicTimeMode, double trendPrice, double trendExtreme, double trendReversal, double trendDelta, double targetDelta, double lambda, int tradingDays, double threshold, double upTrendTrigger, double downTrendTrigger, string tradeState, double bandLevel, double reversalLevel) : IBindValue
{
    public object Bind() => new object?[] { contractId, yearMonth, valueDate, timePeriod, sequenceId, intrinsicTime, intrinsicTimeGroupId, intrinsicTimeLength, intrinsicPrice, intrinsicTimeTrend, intrinsicTimeMode, trendPrice, trendExtreme, trendReversal, trendDelta, targetDelta, lambda, tradingDays, threshold, upTrendTrigger, downTrendTrigger, tradeState, bandLevel, reversalLevel };
}
