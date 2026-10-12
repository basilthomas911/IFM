using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertTradeLimitNoMaxLoss(int tradeId, string tradeType, decimal riskMargin, decimal maxProfit, decimal maxReturn, decimal maxLossLimit, decimal minProfitLimit, decimal maxProfitLimit, decimal minProfitTarget, decimal dailyProfitTarget, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { tradeId, tradeType, riskMargin, maxProfit, null, maxReturn, maxLossLimit, minProfitLimit, maxProfitLimit, minProfitTarget, dailyProfitTarget, createdOn, createdBy, updatedOn, updatedBy };
}
