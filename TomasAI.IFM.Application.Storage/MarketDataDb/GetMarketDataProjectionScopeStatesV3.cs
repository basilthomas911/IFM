using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct GetMarketDataProjectionScopeStatesV3(
    string projectionName,
    ICollection<string> scopeKeys) : IBindValue
{
    public object Bind() => new object?[] { projectionName, scopeKeys };
}
