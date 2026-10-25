using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned financial contract: LedgerPostingBatchReceipt; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record LedgerPostingBatchReceipt
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid OperationId { get; init; }
    [Key(2)] public int PortfolioId { get; init; }
    [Key(3)] public int BookId { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(4)] public LedgerPostedTransaction[] Items { get; init; } = [];
    [Key(5)] public string ManifestHash { get; init; } = string.Empty;
    [Key(6)] public string InputHash { get; init; } = string.Empty;
    [Key(7)] public long FinancialRevision { get; init; }
    [Key(8)] public DateTime CommittedAtUtc { get; init; }
    [Key(9)] public Guid CompletedEventId { get; init; }
}
