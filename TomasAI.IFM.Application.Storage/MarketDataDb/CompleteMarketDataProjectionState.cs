using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.MarketDataDb;
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
internal readonly record struct CompleteMarketDataProjectionState(
    string projectionName,
    Guid generation,
    HashSet<Guid> activeOperations,
    long sourceRowCount,
    long projectedRowCount,
    string sourceFingerprint,
    string projectedFingerprint,
    DateTime completedOn,
    HashSet<Guid> expectedActiveOperations) : IBindValue
{
    public object Bind() => new object?[]
    {
        activeOperations,
        sourceRowCount,
        projectedRowCount,
        sourceFingerprint,
        projectedFingerprint,
        completedOn,
        projectionName,
        generation,
        expectedActiveOperations
    };
}
