namespace TomasAI.IFM.Application.MarketData.FinancialModelingPrep;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero has existing contract semantics; adding an alias would change validation and serialization names.")]
public enum FmpImportDataset
{
    Treasury = 1,
    EconomicCalendar = 2
}
