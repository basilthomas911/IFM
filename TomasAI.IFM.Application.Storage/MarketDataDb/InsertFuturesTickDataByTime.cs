using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesTickDataByTime(string contractId, DateOnly valueDate, TimeOnly tickTime, long tickId, decimal price, int size) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickTime, tickId, price, size };
}
