namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public readonly record struct EconomicCalendarCutoverResult(
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
