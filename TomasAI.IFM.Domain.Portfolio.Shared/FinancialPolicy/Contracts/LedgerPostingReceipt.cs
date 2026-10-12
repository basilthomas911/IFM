using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned financial contract: LedgerPostingReceipt; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record LedgerPostingReceipt
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid OperationId { get; init; }
    [Key(2)] public int PortfolioId { get; init; }
    [Key(3)] public int BookId { get; init; }
    [Key(4)] public int FundId { get; init; }
    [Key(5)] public long TransactionId { get; init; }
    [Key(6)] public long? JournalId { get; init; }
    [Key(7)] public string? JournalHash { get; init; }
    [Key(8)] public string InputHash { get; init; } = string.Empty;
    [Key(9)] public long FinancialRevision { get; init; }
    [Key(10)] public DateTime CommittedAtUtc { get; init; }
    [Key(11)] public Guid CompletedEventId { get; init; }
    [Key(12)] public LedgerSourceReference Source { get; init; } = new();
    [Key(13)] public Guid? ObligationId { get; init; }
}
