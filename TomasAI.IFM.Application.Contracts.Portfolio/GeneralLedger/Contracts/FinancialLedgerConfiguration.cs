using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
/// <summary>Bounded current ledger controls at one authoritative financial revision, for configured selectors.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
[MessagePackObject]
public sealed record FinancialLedgerConfiguration([property: Key(0)] int BookId,
    [property: Key(1)] string Currency, [property: Key(2)] string Environment, [property: Key(3)] string OperatingState,
    [property: Key(4)] string SourceWatermark, [property: Key(5)] FinancialLedgerPeriod[] Periods,
    [property: Key(6)] FinancialConfiguredAccount[] Accounts, [property: Key(7)] FinancialConfiguredRule[] Rules,
    [property: Key(8)] LedgerReconciliationResult? LatestReconciliation,
    [property: Key(9)] FinancialBookConfiguration? DevelopmentQualificationBook = null);
