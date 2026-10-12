using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateTradePosition(int orderId, int tradeId, DateOnly valueDate, string tradeStatus, int daysToExpiry, string tradeType, decimal commission, int deltaHedge, decimal netSpread, decimal tradeValue, decimal tradePnl, decimal assetPrice, double OTMProbability, double winRatio, decimal maxPrice, double hedgeProbability, double riskFreeRate, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { commission, deltaHedge, netSpread, tradeValue, tradePnl, assetPrice, OTMProbability, null, null, riskFreeRate, updatedOn, updatedBy, orderId, tradeId, valueDate, tradeStatus, daysToExpiry, tradeType };
}
