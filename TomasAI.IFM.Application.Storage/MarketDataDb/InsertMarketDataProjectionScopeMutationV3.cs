using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct InsertMarketDataProjectionScopeMutationV3(
    string projectionName,
    string scopeKey,
    Guid mutationId,
    DateTime startedOn) : IBindValue
{
    public object Bind() => new object?[] { projectionName, scopeKey, mutationId, startedOn };
}
