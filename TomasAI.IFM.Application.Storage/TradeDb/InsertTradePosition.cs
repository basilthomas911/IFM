using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertTradePosition(int orderId, int tradeId, string tradeType, DateOnly valueDate, int daysToExpiry, string tradeStatus, decimal commission, int deltaHedge, decimal netSpread, decimal tradeValue, decimal tradePnl, decimal assetPrice, double otmProbability, decimal forwardPrice, double forwardLossRatio, double lossProbability, double riskFreeRate, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, tradeType, valueDate, daysToExpiry, tradeStatus, commission, deltaHedge, netSpread, tradeValue, tradePnl, assetPrice, otmProbability, forwardPrice, forwardLossRatio, lossProbability, riskFreeRate, createdOn, createdBy, updatedOn, updatedBy };
}
