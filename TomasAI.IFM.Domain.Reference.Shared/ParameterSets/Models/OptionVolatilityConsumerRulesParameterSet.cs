namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Immutable authoring contract for option-volatility consumer safeguards and optional rules.</summary>
public sealed record OptionVolatilityConsumerRulesParameterSet
{
    public int SchemaVersion { get; init; } = 1;
    public Guid ParameterSetId { get; init; }
    public int Version { get; init; } = 1;
    public string SeriesId { get; init; } = "ES-ATM-30D";
    public string MethodologyVersion { get; init; } = "methodology-v1";
    public string MetricPolicyVersion { get; init; } = "metric-policy-v1";
    public VolatilityEvidenceRequirement OpenMarketEvidence { get; init; } = VolatilityEvidenceRequirement.Optional;
    public int LiveMaximumAgeMinutes { get; init; } = 15;
    public ClosedMarketEvidencePolicy ClosedMarketEvidence { get; init; } = ClosedMarketEvidencePolicy.LatestQualifiedSession;
    public int MaximumSessionLag { get; init; } = 1;
    public UnavailableVolatilityPolicy UnavailablePolicy { get; init; } = UnavailableVolatilityPolicy.NeverTreatAsZero;
    public bool ExactDecisionSnapshotRequired { get; init; } = true;
    public bool AllowDownstreamSilentReplacement { get; init; }
    public OptionVolatilityNumericalRules NumericalRules { get; init; } = new();
    public VolatilityStrategyPolicy IronCondor { get; init; } = VolatilityStrategyPolicy.Optional;
    public VolatilityStrategyPolicy VerticalSpread { get; init; } = VolatilityStrategyPolicy.Optional;
    public VolatilityStrategyPolicy FuturesOutright { get; init; } = VolatilityStrategyPolicy.Disabled;
    public HeldOptionVolatilityPolicy HeldOptions { get; init; } = HeldOptionVolatilityPolicy.ObserveOnly;
    public ProtectiveActionPolicy ProtectiveClose { get; init; } = ProtectiveActionPolicy.Independent;
    public ProtectiveActionPolicy ProtectiveCancel { get; init; } = ProtectiveActionPolicy.Independent;
}
