using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned financial contract: LedgerPostingRequest; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record LedgerPostingRequest
{
    [Key(0)] public int BookId { get; init; }
    [Key(1)] public int FundId { get; init; }
    [Key(2)] public LedgerTransactionKind TransactionKind { get; init; }
    [Key(3)] public DateOnly AccountingDate { get; init; }
    [Key(4)] public DateOnly ValueDate { get; init; }
    [Key(5)] public DateOnly? SettlementDate { get; init; }
    [Key(6)] public string Currency { get; init; } = string.Empty;
    [Key(7)] public decimal Amount { get; init; }
    [Key(8)] public string Description { get; init; } = string.Empty;
    [Key(9)] public LedgerSourceReference Source { get; init; } = new();
    [Key(10)] public LedgerPostingRuleReference PostingRule { get; init; } = new();
    [Key(11)] public long? RelatedJournalId { get; init; }
    [Key(12)] public int? CounterpartyFundId { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(13)] public LedgerEntryDraft[] Lines { get; init; } = [];
    [Key(14)] public FinancialAuthorityReference Authority { get; init; } = new();
    [Key(15)] public LedgerMovementEvidence MovementEvidence { get; init; } = new();
    [Key(16)] public Guid? RelatedObligationId { get; init; }
    [Key(17)] public Guid? CapacityReservationId { get; init; }
    [Key(18)] public CapacityFundingComponent FundingComponent { get; init; }
}
