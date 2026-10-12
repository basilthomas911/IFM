namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

public readonly record struct MarketDataProjectionBackfillReadModel(
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
        string.Equals(FuturesTicksSourceFingerprint,FuturesTicksProjectedFingerprint,StringComparison.Ordinal) &&
        FuturesEodRowsSource == FuturesEodRowsProjected &&
        string.Equals(FuturesEodSourceFingerprint,FuturesEodProjectedFingerprint,StringComparison.Ordinal) &&
        VixContractsSource == VixContractsIndexed &&
        string.Equals(VixContractsSourceFingerprint,VixContractsIndexedFingerprint,StringComparison.Ordinal) &&
        FuturesItiSignalsSource == FuturesItiSignalsByDayProjected &&
        FuturesItiSignalsSource == FuturesItiSignalsByMonthProjected &&
        FuturesItiSignalsSource == FuturesItiSignalsByTrendModeProjected &&
        string.Equals(FuturesItiSignalsSourceFingerprint,FuturesItiSignalsByDayFingerprint,StringComparison.Ordinal) &&
        string.Equals(FuturesItiSignalsSourceFingerprint,FuturesItiSignalsByMonthFingerprint,StringComparison.Ordinal) &&
        string.Equals(FuturesItiSignalsSourceFingerprint,FuturesItiSignalsByTrendModeFingerprint,StringComparison.Ordinal);
}
