using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetTradePlanForwardLossLimit(int orderId, int tradeId, DateOnly valueDate, string tradeType) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, valueDate, tradeType };
}
