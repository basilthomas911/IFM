namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct MarketDataProjectionReadinessReadModel(
    bool FuturesTickByTime,
    bool FuturesEodByMonth,
    bool VixFuturesContractIndex,
    bool FuturesItiSignalQueries)
{
    public bool IsReady => FuturesTickByTime && FuturesEodByMonth &&
        VixFuturesContractIndex && FuturesItiSignalQueries;
}
