using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned financial contract: LedgerPostingBatchRequest; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record LedgerPostingBatchRequest
{
    [Key(0)] public int BookId { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(1)] public LedgerPostingRequest[] Items { get; init; } = [];
    [Key(2)] public string BatchSourceReference { get; init; } = string.Empty;
    [Key(3)] public string ManifestHash { get; init; } = string.Empty;
}
