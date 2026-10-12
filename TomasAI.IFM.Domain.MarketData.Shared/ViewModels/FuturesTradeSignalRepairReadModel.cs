namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>Summary of the non-destructive Futures Trade Signal repair.</summary>
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct FuturesTradeSignalRepairReadModel(
    long RowsScanned,
    long ValidRows,
    long QuarantinedRows,
    long LookupRowsWritten);
