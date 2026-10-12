using MessagePack;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Exact committed capacity usage deltas, including whole-position slot normalization and rounding.</summary>
[MessagePackObject]
public sealed record AcceptedCapacityUsage([property: Key(0)] CapacityScopeKind ScopeKind,[property: Key(1)] string ScopeKey,
    [property: Key(2)] CapacityMeasure Measure,[property: Key(3)] CapacityUnit Unit,
    [property: Key(4)] decimal Held,[property: Key(5)] decimal Working,[property: Key(6)] decimal Position);
