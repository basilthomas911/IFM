using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

[MessagePackObject]
public sealed record RiskLimitCheck([property:Key(0)] int LimitIndex, [property:Key(1)] decimal Proposed,
    [property:Key(2)] decimal Existing, [property:Key(3)] bool Fits);
[MessagePackObject]
public sealed record RiskQuantityCheck([property:Key(0)] int Units, [property:Key(1)] decimal Cash,
    [property:Key(2)] decimal Loss, [property:Key(3)] bool CashFits, [property:Key(4)] bool LossFits,
    [property:Key(5)] ImmutableArray<RiskLimitCheck> Limits);

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
