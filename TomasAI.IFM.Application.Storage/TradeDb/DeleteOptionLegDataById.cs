using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct DeleteOptionLegDataById(int orderId, int tradeId, DateOnly valueDate, string tradeType, int daysToExpiry, string tradeStatus, string optionLegId) : IBindValue
{
    public object Bind() => new object?[] { orderId, tradeId, valueDate, optionLegId };
}
