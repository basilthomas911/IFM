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
    long FuturesItiSignalsSource,
    long FuturesItiSignalsByDayProjected,
    long FuturesItiSignalsByMonthProjected,
    long FuturesItiSignalsByTrendModeProjected,
    string FuturesItiSignalsSourceFingerprint,
    string FuturesItiSignalsByDayFingerprint,
    string FuturesItiSignalsByMonthFingerprint,
    string FuturesItiSignalsByTrendModeFingerprint,
    bool CutoverCompleted)
{
    public bool IsReconciled =>
        FuturesTicksSource == FuturesTicksProjected &&
        FuturesTicksSourceFingerprint == FuturesTicksProjectedFingerprint &&
        FuturesEodRowsSource == FuturesEodRowsProjected &&
        FuturesEodSourceFingerprint == FuturesEodProjectedFingerprint &&
        VixContractsSource == VixContractsIndexed &&
        VixContractsSourceFingerprint == VixContractsIndexedFingerprint &&
        FuturesItiSignalsSource == FuturesItiSignalsByDayProjected &&
        FuturesItiSignalsSource == FuturesItiSignalsByMonthProjected &&
        FuturesItiSignalsSource == FuturesItiSignalsByTrendModeProjected &&
        FuturesItiSignalsSourceFingerprint == FuturesItiSignalsByDayFingerprint &&
        FuturesItiSignalsSourceFingerprint == FuturesItiSignalsByMonthFingerprint &&
        FuturesItiSignalsSourceFingerprint == FuturesItiSignalsByTrendModeFingerprint;
}

public readonly record struct MarketDataProjectionReadiness(
    bool FuturesTickByTime,
    bool FuturesEodByMonth,
    bool VixFuturesContractIndex,
    bool FuturesItiSignalQueries)
{
    public bool IsReady => FuturesTickByTime && FuturesEodByMonth &&
        VixFuturesContractIndex && FuturesItiSignalQueries;
}
