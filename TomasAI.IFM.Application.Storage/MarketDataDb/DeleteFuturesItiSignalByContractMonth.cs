using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesItiSignalByContractMonth(string contractId, int yearMonth, DateOnly valueDate, long sequenceId, string timePeriod, string intrinsicTimeMode, string intrinsicTimeTrend, int intrinsicTimeGroupId) : IBindValue
{
    public object Bind() => new object?[] { contractId, yearMonth, valueDate, sequenceId, timePeriod, intrinsicTimeMode, intrinsicTimeTrend, intrinsicTimeGroupId };
}
