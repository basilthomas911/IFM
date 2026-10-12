using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CompleteMarketDataProjectionScopeOperationV3(
    string projectionName,
    string scopeKey,
    Guid generation,
    HashSet<Guid> activeOperations,
    DateTime completedOn,
    HashSet<Guid> expectedActiveOperations) : IBindValue
{
    public object Bind() => new object?[]
    {
        activeOperations,
        completedOn,
        projectionName,
        scopeKey,
        generation,
        expectedActiveOperations
    };
}
