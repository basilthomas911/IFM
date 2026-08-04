namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public readonly record struct MarketDataProjectionBackfillResult(
    long FuturesTicksSource,
    long FuturesTicksProjected,
    string FuturesTicksSourceFingerprint,
    string FuturesTicksProjectedFingerprint,
    long FuturesEodRowsSource,
    long FuturesEodRowsProjected,
    string FuturesEodSourceFingerprint,
    string FuturesEodProjectedFingerprint,
    long VixFuturesEodRowsSource,
    long VixContractsSource,
    long VixContractsIndexed,
    string VixContractsSourceFingerprint,
    string VixContractsIndexedFingerprint,
    bool CutoverCompleted)
{
    public bool IsReconciled =>
        FuturesTicksSource == FuturesTicksProjected &&
        FuturesTicksSourceFingerprint == FuturesTicksProjectedFingerprint &&
        FuturesEodRowsSource == FuturesEodRowsProjected &&
        FuturesEodSourceFingerprint == FuturesEodProjectedFingerprint &&
        VixContractsSource == VixContractsIndexed &&
        VixContractsSourceFingerprint == VixContractsIndexedFingerprint;
}

public readonly record struct MarketDataProjectionReadiness(
    bool FuturesTickByTime,
    bool FuturesEodByMonth,
    bool VixFuturesContractIndex)
{
    public bool IsReady => FuturesTickByTime && FuturesEodByMonth && VixFuturesContractIndex;
}
