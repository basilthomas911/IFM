namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

public readonly record struct EconomicCalendarCutoverReadModel(
    long SourceRows,
    long TargetRows,
    string SourceFingerprint,
    string TargetFingerprint,
    int CountryCodes,
    bool CutoverCompleted)
{
    public bool IsReconciled =>
        SourceRows == TargetRows &&
        string.Equals(SourceFingerprint, TargetFingerprint, StringComparison.Ordinal) &&
        CutoverCompleted;
}

/// <summary>Reconciliation result for the remaining yield-curve query projections.</summary>
public readonly record struct FmpQueryProjectionBackfillReadModel(
    long YieldCurveRowsSource,
    long YieldCurveRowsProjected,
    string YieldCurveSourceFingerprint,
    string YieldCurveProjectedFingerprint,
    int YieldCurveYearsSource,
    int YieldCurveYearsProjected,
    string YieldCurveYearsSourceFingerprint,
    string YieldCurveYearsProjectedFingerprint)
{
    public bool IsReconciled =>
        YieldCurveRowsSource == YieldCurveRowsProjected &&
        YieldCurveSourceFingerprint == YieldCurveProjectedFingerprint &&
        YieldCurveYearsSource == YieldCurveYearsProjected &&
        YieldCurveYearsSourceFingerprint == YieldCurveYearsProjectedFingerprint;
}

/// <summary>Summary of the non-destructive Futures Trade Signal repair.</summary>
public readonly record struct FuturesTradeSignalRepairReadModel(
    long RowsScanned,
    long ValidRows,
    long QuarantinedRows,
    long LookupRowsWritten);

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

public readonly record struct MarketDataProjectionReadinessReadModel(
    bool FuturesTickByTime,
    bool FuturesEodByMonth,
    bool VixFuturesContractIndex,
    bool FuturesItiSignalQueries)
{
    public bool IsReady => FuturesTickByTime && FuturesEodByMonth &&
        VixFuturesContractIndex && FuturesItiSignalQueries;
}
