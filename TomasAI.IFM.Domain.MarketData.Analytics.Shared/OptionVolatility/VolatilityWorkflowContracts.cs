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

/// <summary>
/// Frozen input used by a trading consumer.  The optional snapshot remains null when an optional
/// dependency is unavailable; absence is never represented by a numeric zero.
/// </summary>
[MessagePackObject]
public sealed record VolatilityWorkflowInput
{
    [Key(0)] public ushort SchemaVersion { get; init; } = 1;
    [Key(1)] public VolatilityWorkflowDependencyPolicy Dependency { get; init; } = default!;
    [Key(2)] public OptionIvMetricSnapshot? Snapshot { get; init; }
    [Key(3)] public VolatilityFreshnessStatus FreshnessStatus { get; init; }
    [Key(4)] public DateTimeOffset EvaluatedAtUtc { get; init; }
    [Key(5)] public string QualificationReasonCode { get; init; } = string.Empty;

    [IgnoreMember]
    public VolatilityWorkflowEvidence? AcceptedEvidence => Snapshot is null ? null : new(
        VolatilityWorkflowEvidence.CurrentSchemaVersion,
        Snapshot.SnapshotId,
        Snapshot.SnapshotDigest,
        Snapshot.Series,
        Snapshot.MetricPolicyVersion,
        Dependency.DependencyPolicyId,
        Dependency.DependencyPolicyVersion,
        Dependency.Requirement,
        EvaluatedAtUtc,
        FreshnessStatus,
        QualificationReasonCode);
}

public sealed record VolatilityWorkflowGateResult(bool AllowsNewEntry, string ReasonCode);

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
