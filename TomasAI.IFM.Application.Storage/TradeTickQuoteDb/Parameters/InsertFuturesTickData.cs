using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesTickData(string contractId, DateOnly valueDate, long tickId, TimeOnly tickTime, decimal price, int size) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickId, tickTime, price, size };
}
