using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

/// <summary>Versioned declaration of whether a workflow can proceed without volatility evidence.</summary>
[MessagePackObject]
public sealed record VolatilityWorkflowDependencyPolicy(
    [property: Key(0)] ushort SchemaVersion,
    [property: Key(1)] string DependencyPolicyId,
    [property: Key(2)] string DependencyPolicyVersion,
    [property: Key(3)] VolatilitySeriesIdentity Series,
    [property: Key(4)] string MetricPolicyVersion,
    [property: Key(5)] VolatilityDependencyRequirement Requirement)
{
    public const ushort CurrentSchemaVersion = 1;
}
