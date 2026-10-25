using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

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
