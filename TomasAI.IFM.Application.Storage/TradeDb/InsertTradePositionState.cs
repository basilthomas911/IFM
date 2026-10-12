using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertTradePositionState(int orderId, int tradeId, string tradePositionState, DateTime openedOn, string openedBy) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, tradePositionState, openedOn, openedBy };
}
