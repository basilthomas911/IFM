using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertOptionTrade(int tradeId, int orderId, DateOnly tradeDate, DateOnly maturityDate, string tradeType, string tradeState, string tradeStrategy, string tradeAction, string underlyingContractId, string underlyingAssetType, bool isPrimaryTrade, bool isHedgeTrade, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, tradeDate, maturityDate, tradeType, tradeState, tradeStrategy, tradeAction, underlyingContractId, underlyingAssetType, isPrimaryTrade, isHedgeTrade, createdOn, createdBy, updatedOn, updatedBy };
}
