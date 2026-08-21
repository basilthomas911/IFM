namespace TomasAI.IFM.Application.Storage.MarketDataDb;

/// <summary>Summary of the non-destructive Futures Trade Signal repair.</summary>
public readonly record struct FuturesTradeSignalRepairResult(
    long RowsScanned,
    long ValidRows,
    long QuarantinedRows,
    long LookupRowsWritten);
