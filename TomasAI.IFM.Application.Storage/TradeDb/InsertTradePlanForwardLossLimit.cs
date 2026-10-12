using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertTradePlanForwardLossLimit(int orderId, int tradeId, DateOnly valueDate, string tradeType, string limitType) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, tradeType, valueDate, limitType };
}
