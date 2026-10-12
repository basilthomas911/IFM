using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

/// <summary>Exact accepted volatility evidence attached to a workflow decision.</summary>
[MessagePackObject]
public sealed record VolatilityWorkflowEvidence(
    [property: Key(0)] ushort SchemaVersion,
    [property: Key(1)] string SnapshotId,
    [property: Key(2)] string SnapshotDigest,
    [property: Key(3)] VolatilitySeriesIdentity Series,
    [property: Key(4)] string MetricPolicyVersion,
    [property: Key(5)] string DependencyPolicyId,
    [property: Key(6)] string DependencyPolicyVersion,
    [property: Key(7)] VolatilityDependencyRequirement Requirement,
    [property: Key(8)] DateTimeOffset DecisionAtUtc,
    [property: Key(9)] VolatilityFreshnessStatus FreshnessStatus,
    [property: Key(10)] string RuleOutcomeCode)
{
    public const ushort CurrentSchemaVersion = 1;
}
