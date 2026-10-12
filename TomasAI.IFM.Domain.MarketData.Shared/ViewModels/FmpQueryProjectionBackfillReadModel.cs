namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

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
        (string.Equals((YieldCurveSourceFingerprint),(YieldCurveProjectedFingerprint),StringComparison.Ordinal)) &&
        YieldCurveYearsSource == YieldCurveYearsProjected &&
        (string.Equals((YieldCurveYearsSourceFingerprint),(YieldCurveYearsProjectedFingerprint),StringComparison.Ordinal));
}
