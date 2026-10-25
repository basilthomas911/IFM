namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct MarketDataProjectionReadinessReadModel(
    bool FuturesTickByTime,
    bool FuturesEodByMonth,
    bool VixFuturesContractIndex,
    bool FuturesItiSignalQueries)
{
    /// <summary>Raw tick query maintenance is outside MarketDataDb; tick flags/counts are not a Scylla health claim.</summary>
    public bool RawTicksManagedByTradeTickQuoteDb { get; init; }

    public bool IsReady => (RawTicksManagedByTradeTickQuoteDb || FuturesTickByTime) && FuturesEodByMonth &&
        VixFuturesContractIndex && FuturesItiSignalQueries;
}
