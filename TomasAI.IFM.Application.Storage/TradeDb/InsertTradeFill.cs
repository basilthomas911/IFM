using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertTradeFill(int orderId, int tradeId, DateTime fillDate, int fillQuantity, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, fillDate, fillQuantity, createdOn, createdBy };
}
