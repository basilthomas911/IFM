namespace TomasAI.IFM.Application.MarketData.FinancialModelingPrep;

public sealed record FmpMarketDataImportRequest(
    DateOnly FromInclusive,
    DateOnly ToInclusive,
    bool IncludeTreasury = true,
    bool IncludeEconomicCalendar = true,
    string[]? CountryCodes = null);

public enum FmpImportDataset
{
    Treasury = 1,
    EconomicCalendar = 2
}

public enum FmpImportDateStatus
{
    Submitted = 1,
    Rejected = 2
}

public sealed record FmpImportDateResult(
    DateOnly Date,
    FmpImportDataset Dataset,
    FmpImportDateStatus Status,
    Guid? CommandId,
    int ErrorCode,
    string? ErrorMessage);

public sealed record FmpMarketDataImportResult(
    DateOnly FromInclusive,
    DateOnly ToInclusive,
    int RequestedDatasetDates,
    int SubmittedCommands,
    int RejectedSubmissions,
    int RemainingUnsubmittedDates,
    bool Cancelled,
    IReadOnlyList<FmpImportDateResult> Dates);

public sealed class FmpMarketDataImportOptions
{
    public int MaximumRangeDays { get; set; } = 366;

    internal void Validate()
    {
        if (MaximumRangeDays <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumRangeDays));
        }
    }
}

public sealed class FmpMarketDataImportException : Exception
{
    public FmpMarketDataImportException(string message)
        : base(message)
    {
    }
}
