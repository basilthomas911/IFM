using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
[MessagePackObject]
public sealed record LedgerReconciliationResult([property: Key(0)] Guid ReconciliationId, [property: Key(1)] long FinancialRevision,
    [property: Key(2)] long JournalCount, [property: Key(3)] long EntryCount, [property: Key(4)] decimal Debits,
    [property: Key(5)] decimal Credits, [property: Key(6)] LedgerBalanceDifference[] Differences,
    [property: Key(7)] string SourceCut, [property: Key(8)] string ContentHash);
