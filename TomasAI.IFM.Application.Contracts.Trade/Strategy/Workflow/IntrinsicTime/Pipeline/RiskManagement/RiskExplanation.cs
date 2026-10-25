using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

/// <summary>Versioned sidecar created from the accepted immutable invocation, without changing historic result hashes.</summary>
[MessagePackObject]
public sealed record RiskExplanation
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public string ResultHash { get; init; } = "";
    [Key(2)] public int MaximumUnits { get; init; }
    [Key(3)] public decimal AvailableCash { get; init; }
    [Key(4)] public decimal EffectiveLossBudget { get; init; }
    [Key(5)] public decimal MarketMultiplier { get; init; }
    [Key(6)] public ImmutableArray<string> Conditions { get; init; } = [];
    [Key(7)] public ImmutableArray<CapacityLimit> Limits { get; init; } = [];
    [Key(8)] public ImmutableArray<RiskQuantityCheck> Quantities { get; init; } = [];
    [Key(9)] public string ContentHash { get; init; } = "";
    public string Hash() => RiskContracts.Hash(this with { ContentHash = "" });
}
