using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned financial contract: LedgerPostedTransaction; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record LedgerPostedTransaction
{
    [Key(0)] public int Ordinal { get; init; }
    [Key(1)] public long TransactionId { get; init; }
    [Key(2)] public long? JournalId { get; init; }
    [Key(3)] public string? JournalHash { get; init; }
    [Key(4)] public LedgerSourceReference Source { get; init; } = new();
    [Key(5)] public Guid? ObligationId { get; init; }
}
