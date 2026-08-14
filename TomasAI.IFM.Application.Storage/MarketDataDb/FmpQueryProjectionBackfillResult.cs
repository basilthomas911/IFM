namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>
/// Reconciliation result for the bounded economic-calendar and yield-curve query projections.
/// </summary>
public readonly record struct FmpQueryProjectionBackfillResult(
    long EconomicCalendarRowsSource,
    long EconomicCalendarRowsProjected,
    string EconomicCalendarSourceFingerprint,
    string EconomicCalendarProjectedFingerprint,
    int EconomicCalendarCountryCodesSource,
    int EconomicCalendarCountryCodesProjected,
    string EconomicCalendarCountryCodesSourceFingerprint,
    string EconomicCalendarCountryCodesProjectedFingerprint,
    int EconomicCalendarMonthsSource,
    int EconomicCalendarMonthsProjected,
    string EconomicCalendarMonthsSourceFingerprint,
    string EconomicCalendarMonthsProjectedFingerprint,
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
        EconomicCalendarRowsSource == EconomicCalendarRowsProjected &&
        EconomicCalendarSourceFingerprint == EconomicCalendarProjectedFingerprint &&
        EconomicCalendarCountryCodesSource == EconomicCalendarCountryCodesProjected &&
        EconomicCalendarCountryCodesSourceFingerprint == EconomicCalendarCountryCodesProjectedFingerprint &&
        EconomicCalendarMonthsSource == EconomicCalendarMonthsProjected &&
        EconomicCalendarMonthsSourceFingerprint == EconomicCalendarMonthsProjectedFingerprint &&
        YieldCurveRowsSource == YieldCurveRowsProjected &&
        YieldCurveSourceFingerprint == YieldCurveProjectedFingerprint &&
        YieldCurveYearsSource == YieldCurveYearsProjected &&
        YieldCurveYearsSourceFingerprint == YieldCurveYearsProjectedFingerprint;
}
