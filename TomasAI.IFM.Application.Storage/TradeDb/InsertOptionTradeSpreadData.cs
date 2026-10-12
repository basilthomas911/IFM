using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertOptionTradeSpreadData(int orderId, int tradeId, DateOnly valueDate, string tradeType, long sequenceId, decimal lossLimit, decimal winLimit, decimal forwardSpread, decimal netSpread, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, tradeType, valueDate, sequenceId, lossLimit, winLimit, forwardSpread, netSpread, createdOn, createdBy };
}
