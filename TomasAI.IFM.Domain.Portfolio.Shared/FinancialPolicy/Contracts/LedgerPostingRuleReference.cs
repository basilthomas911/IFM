using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned financial contract: LedgerPostingRuleReference; numeric keys are append-only.</summary>
[MessagePackObject]
public sealed record LedgerPostingRuleReference
{
    [Key(0)] public Guid RuleId { get; init; }
    [Key(1)] public int Version { get; init; }
    [Key(2)] public string ContentHash { get; init; } = string.Empty;
}
