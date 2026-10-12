using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateTradeLiveFeed(int orderId, int tradeId, bool liveFeed) : IBindValue
{
    public object Bind() => new object?[] { liveFeed, orderId, tradeId };
}
