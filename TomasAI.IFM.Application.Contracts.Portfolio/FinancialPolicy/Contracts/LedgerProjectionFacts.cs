using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Accepted posting facts for deterministic reconstruction without pricing or admission rules.</summary>
[MessagePackObject]
public sealed record LedgerProjectionFacts
{
    [Key(0)] public int Version { get; init; } = 1;
    [Key(1)] public string ActorName { get; init; } = string.Empty;
    [Key(2)] public string ExecutionId { get; init; } = string.Empty;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(3)] public AcceptedLedgerPosting[] Items { get; init; } = [];
}
