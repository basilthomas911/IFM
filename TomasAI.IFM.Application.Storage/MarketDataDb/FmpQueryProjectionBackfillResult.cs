namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Reconciliation result for the remaining yield-curve query projections.</summary>
public readonly record struct FmpQueryProjectionBackfillResult(
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
