using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

// Read query parameters
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetOptionTrades(int orderId) : IBindValue
{
    public object Bind() => new object?[] { orderId };
}
