using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct UpdateOptionLegDataStatus(int tradeId, DateOnly valueDate, string OptionLegId, string newTradeStatus, DateTime updatedOn, string updatedBy) : IBindValue
{
    public object Bind() => new object?[] { newTradeStatus, updatedOn, updatedBy, tradeId, valueDate, OptionLegId };
}
