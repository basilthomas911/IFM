using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateFuturesEodSessionStatistics(
    string contractId,
    DateOnly valueDate,
    string symbol,
    decimal openPrice,
    decimal highPrice,
    decimal lowPrice,
    long volume,
    double dailyPercentChange,
    string priceDirection) : IBindValue
{
    public object Bind() => new object?[]
    {
        openPrice,
        highPrice,
        lowPrice,
        volume,
        dailyPercentChange,
        priceDirection,
        contractId,
        valueDate,
        symbol
    };
}
