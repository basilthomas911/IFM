using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

// Delete parameters
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteOptionTrade(int orderId, int tradeId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId };
}
