using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteTradePositionByPrimaryKey(int orderId, int tradeId, string tradeType, DateOnly valueDate, int daysToExpiry, string tradeStatus) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, valueDate, tradeStatus, daysToExpiry, tradeType };
}
