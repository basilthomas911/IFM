using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

// Update parameters
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateOptionTradeState(int orderId, int tradeId, string tradeState, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { tradeState, orderId, tradeId };
}
