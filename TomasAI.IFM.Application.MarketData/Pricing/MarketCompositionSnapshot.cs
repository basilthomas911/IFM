using System.Collections.Immutable;
using MessagePack;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Pricing;

[MessagePackObject]
public sealed record MarketCompositionSnapshot(
    [property: Key(0)] int SchemaVersion,
    [property: Key(1)] Guid SnapshotId,
    [property: Key(2)] string ScopeId,
    [property: Key(3)] string ScopeToken,
    [property: Key(4)] string Horizon,
    [property: Key(5)] Guid GenerationId,
    [property: Key(6)] DateTimeOffset EvaluatedAtUtc,
    [property: Key(7)] DateTimeOffset ValidUntilUtc,
    [property: Key(8)] ImmutableArray<CompositionInstrumentSnapshot> Instruments,
    [property: Key(9)] string Digest)
{
    /// <summary>Exact global policy used to prepare this ranking universe; absent on historical/manual snapshots.</summary>
    [Key(10), System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? StrategyOptionChainParametersJson { get; init; }
    /// <summary>Normalized prepared direction, separate from catalog Balanced spelling.</summary>
    [Key(11), System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? StrategyOptionChainBias { get; init; }
    /// <summary>Accepted operational value date of the prepared strategy scope.</summary>
    [Key(12), System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? StrategyOptionChainValueDate { get; init; }
}
