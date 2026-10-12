namespace TomasAI.IFM.Application.MarketData.FinancialModelingPrep;

public sealed record FmpMarketDataImportResult(
    DateOnly FromInclusive,
    DateOnly ToInclusive,
    int RequestedDatasetDates,
    int SubmittedCommands,
    int RejectedSubmissions,
    int RemainingUnsubmittedDates,
    bool Cancelled,
    IReadOnlyList<FmpImportDateResult> Dates);
