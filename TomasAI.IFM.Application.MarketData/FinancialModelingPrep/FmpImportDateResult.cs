namespace TomasAI.IFM.Application.MarketData.FinancialModelingPrep;

public sealed record FmpImportDateResult(
    DateOnly Date,
    FmpImportDataset Dataset,
    FmpImportDateStatus Status,
    Guid? CommandId,
    int ErrorCode,
    string? ErrorMessage);
