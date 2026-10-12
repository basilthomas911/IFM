using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetCurrentFuturesEodDataByDateRange(int yearMonth, DateOnly startDate, DateOnly endDate) : IBindValue
{
    public object Bind() => new object?[] { yearMonth, startDate, endDate };
}
