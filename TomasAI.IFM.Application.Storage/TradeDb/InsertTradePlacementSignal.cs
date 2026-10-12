using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertTradePlacementSignal(long sequenceId, string contractId, DateOnly valueDate, string tradePlacementSignal, decimal tradePrice, DateTime createdOn, string createdBy) : IBindValue
{
    public object Bind() => new object?[] { sequenceId, contractId, valueDate, tradePlacementSignal, tradePrice, createdOn, createdBy };
}
