using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertOptionTradeSpreadBarData(int orderId, int tradeId, string tradeType, DateOnly valueDate, DateTime barDate, decimal lossLimit, decimal winLimit, decimal forwardSpread, decimal netSpread) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, tradeType, valueDate, barDate, lossLimit, winLimit, forwardSpread, netSpread };
}
