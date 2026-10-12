using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetTradePlanForwardLossRatios(DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { startDate, endDate };
}
