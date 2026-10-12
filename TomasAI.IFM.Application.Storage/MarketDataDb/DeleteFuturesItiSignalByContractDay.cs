using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteFuturesItiSignalByContractDay(string contractId, DateOnly valueDate, string intrinsicTimeMode, long sequenceId, string timePeriod, string intrinsicTimeTrend, int intrinsicTimeGroupId) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, intrinsicTimeMode, sequenceId, timePeriod, intrinsicTimeTrend, intrinsicTimeGroupId };
}
