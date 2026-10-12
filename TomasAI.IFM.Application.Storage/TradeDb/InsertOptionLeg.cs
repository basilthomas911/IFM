using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

// Insert parameters
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertOptionLeg(int orderId, int tradeId, string contractId, int quantity, decimal strikePrice, string optionLegType, string optionLegAction, DateTime createdOn, string createdBy, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, contractId, quantity, strikePrice, optionLegType, optionLegAction, createdOn, createdBy, updatedOn, updatedBy };
}
