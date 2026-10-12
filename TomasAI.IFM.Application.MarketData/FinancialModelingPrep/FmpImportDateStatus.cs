namespace TomasAI.IFM.Application.MarketData.FinancialModelingPrep;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero has existing contract semantics; adding an alias would change validation and serialization names.")]
public enum FmpImportDateStatus
{
    Submitted = 1,
    Rejected = 2
}
