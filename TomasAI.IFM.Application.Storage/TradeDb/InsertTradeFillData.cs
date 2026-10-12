using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertTradeFillData(int orderId, int tradeId, string contractId, DateTime fillDate, decimal bidPrice, decimal askPrice, decimal commission, string optionLegAction, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, contractId, fillDate, bidPrice, askPrice, commission, optionLegAction, createdOn, createdBy };
}
