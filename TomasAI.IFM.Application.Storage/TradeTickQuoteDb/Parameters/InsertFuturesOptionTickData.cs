using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertFuturesOptionTickData(string contractId, DateOnly valueDate, long tickId, TimeOnly tickTime, double optionPrice, double bidPrice, double askPrice, int bidSize, int askSize, double impliedVolatility, double underlyingPrice, double delta, double gamma, double vega, double theta, double rho) : IBindValue
{
    public object Bind() => new object?[] { contractId, valueDate, tickId, tickTime, optionPrice, bidPrice, askPrice, bidSize, askSize, impliedVolatility, underlyingPrice, delta, gamma, vega, theta, rho };
}
