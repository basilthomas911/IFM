using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetTradePosition(int orderId, int tradeId, DateOnly valueDate, string tradeStatus, int daysToExpiry, string tradeType) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, valueDate, tradeStatus, daysToExpiry, tradeType };
}
