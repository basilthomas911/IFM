using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertTradeTypeLimit(int tradeId, string tradeType, decimal maxLossLimit, decimal minProfitLimit, decimal maxProfitLimit) : IBindValue
{
    public object Bind() => new object?[] { tradeId, tradeType, maxLossLimit, minProfitLimit, maxProfitLimit };
}
