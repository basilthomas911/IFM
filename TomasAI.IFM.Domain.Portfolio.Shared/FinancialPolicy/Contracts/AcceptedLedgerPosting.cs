using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Exact resolved journal and associated financial effects of one committed posting.</summary>
[MessagePackObject]
public sealed record AcceptedLedgerPosting
{
    [Key(0)] public int Ordinal { get; init; }
    [Key(1)] public long TransactionId { get; init; }
    [Key(2)] public long? JournalId { get; init; }
    [Key(3)] public string? JournalHash { get; init; }
    [Key(4)] public LedgerPostingRequest Request { get; init; } = new();
    [Key(5)] public Guid RuleId { get; init; }
    [Key(6)] public long RuleVersion { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(7)] public AcceptedLedgerLine[] Lines { get; init; } = [];
    [Key(8)] public Guid? ObligationId { get; init; }
    [Key(9)] public decimal? FundingCashPaid { get; init; }
    [Key(10)] public bool BecameOverdrawn { get; init; }
}
