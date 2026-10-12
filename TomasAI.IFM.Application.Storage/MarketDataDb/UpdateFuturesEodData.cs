using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateFuturesEodData(string contractId, DateOnly valueDate, string symbol, decimal openPrice, decimal highPrice, decimal lowPrice, decimal closePrice, long volume, double dailyPercentChange, double dailyStdDev, double dailyStdDevAmount, double upperBand, double mean, double lowerBand, string marketDirection, string marketVolatility, string priceDirection, string priceVolatility, double marketDirectionIndicator, int windowSize, decimal fiftyDMA, decimal twoHundredDMA) : IBindValue
{
    public object Bind() => new object?[] { openPrice, highPrice, lowPrice, closePrice, volume, dailyPercentChange, dailyStdDev, dailyStdDevAmount, upperBand, mean, lowerBand, marketDirection, marketVolatility, priceDirection, priceVolatility, marketDirectionIndicator, windowSize, fiftyDMA, twoHundredDMA, contractId, valueDate, symbol };
}
