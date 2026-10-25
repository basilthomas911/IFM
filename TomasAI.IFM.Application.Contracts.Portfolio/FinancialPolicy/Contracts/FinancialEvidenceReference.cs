using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned financial contract: FinancialEvidenceReference; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record FinancialEvidenceReference
{
    [Key(0)] public Guid EvidenceId { get; init; }
    [Key(1)] public int Version { get; init; }
    [Key(2)] public string ContentHash { get; init; } = string.Empty;
    [Key(3)] public string Source { get; init; } = string.Empty;
    [Key(4)] public string Environment { get; init; } = string.Empty;
    [Key(5)] public DateTime ObservedAtUtc { get; init; }
    [Key(6)] public DateTime ValidUntilUtc { get; init; }
}
