using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertTradeLimit(int tradeId, string tradeType, decimal riskMargin, decimal maxProfit, decimal maxLoss, decimal maxReturn, decimal maxLossLimit, decimal minProfitLimit, decimal maxProfitLimit, decimal minProfitTarget, decimal dailyProfitTarget, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { tradeId, tradeType, riskMargin, maxProfit, maxLoss, maxReturn, maxLossLimit, minProfitLimit, maxProfitLimit, minProfitTarget, dailyProfitTarget, createdOn, createdBy, updatedOn, updatedBy };
}
