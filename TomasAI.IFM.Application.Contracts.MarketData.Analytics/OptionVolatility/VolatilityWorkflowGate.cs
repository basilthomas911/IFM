using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

/// <summary>Shared deterministic consumer gate. Protective/cancel/closing actions never depend on analytics.</summary>
public static class VolatilityWorkflowGate
{
    public static VolatilityWorkflowGateResult Evaluate(VolatilityWorkflowInput input, bool protectiveOrClosingAction = false)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (protectiveOrClosingAction)
            return new(true, "VOL.PROTECTIVE_ACTION");
        if (input.SchemaVersion != 1 || input.Dependency is null ||
            input.Dependency.SchemaVersion != VolatilityWorkflowDependencyPolicy.CurrentSchemaVersion ||
            input.Dependency.Requirement == VolatilityDependencyRequirement.Unspecified)
            return new(false, "VOL.DEPENDENCY.INVALID");
        if (input.Snapshot is null)
            return input.Dependency.Requirement == VolatilityDependencyRequirement.Optional
                ? new(true, "VOL.OPTIONAL.UNAVAILABLE")
                : new(false, "VOL.REQUIRED.MISSING");
        var snapshot = input.Snapshot;
        if (snapshot.SchemaVersion != OptionIvMetricSnapshot.CurrentSchemaVersion ||
            snapshot.Series != input.Dependency.Series ||
            !StringComparer.Ordinal.Equals(snapshot.MetricPolicyVersion, input.Dependency.MetricPolicyVersion))
            return new(false, "VOL.EVIDENCE.MISMATCH");
        if (input.FreshnessStatus != VolatilityFreshnessStatus.Accepted)
            return new(false, input.FreshnessStatus == VolatilityFreshnessStatus.Stale
                ? "VOL.REQUIRED.STALE" : "VOL.REQUIRED.UNAVAILABLE");
        if (snapshot.CurrentImpliedVolatility is null || snapshot.ImpliedVolatilityUnit != VolatilityValueUnit.AnnualDecimal ||
            snapshot.RankStatus != VolatilityMetricStatus.Qualified ||
            snapshot.PercentileStatus != VolatilityMetricStatus.Qualified ||
            snapshot.IvRank is null || snapshot.IvPercentile is null ||
            snapshot.MetricUnit != VolatilityMetricUnit.PercentagePoints0To100)
            return new(false, "VOL.REQUIRED.UNQUALIFIED");
        return new(true, "VOL.ACCEPTED");
    }
}
