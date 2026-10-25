namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Immutable authoring contract for option-volatility consumer safeguards and optional rules.</summary>
[MessagePack.MessagePackObject]
public sealed record OptionVolatilityConsumerRulesParameterSet
{
    [MessagePack.Key(0)] public int SchemaVersion { get; init; } = 1;
    [MessagePack.Key(1)] public Guid ParameterSetId { get; init; }
    [MessagePack.Key(2)] public int Version { get; init; } = 1;
    [MessagePack.Key(3)] public string SeriesId { get; init; } = "ES-ATM-30D";
    [MessagePack.Key(4)] public string MethodologyVersion { get; init; } = "methodology-v1";
    [MessagePack.Key(5)] public string MetricPolicyVersion { get; init; } = "metric-policy-v1";
    [MessagePack.Key(6)] public VolatilityEvidenceRequirement OpenMarketEvidence { get; init; } = VolatilityEvidenceRequirement.Optional;
    [MessagePack.Key(7)] public int LiveMaximumAgeMinutes { get; init; } = 15;
    [MessagePack.Key(8)] public ClosedMarketEvidencePolicy ClosedMarketEvidence { get; init; } = ClosedMarketEvidencePolicy.LatestQualifiedSession;
    [MessagePack.Key(9)] public int MaximumSessionLag { get; init; } = 1;
    [MessagePack.Key(10)] public UnavailableVolatilityPolicy UnavailablePolicy { get; init; } = UnavailableVolatilityPolicy.NeverTreatAsZero;
    [MessagePack.Key(11)] public bool ExactDecisionSnapshotRequired { get; init; } = true;
    [MessagePack.Key(12)] public bool AllowDownstreamSilentReplacement { get; init; }
    [MessagePack.Key(13)] public OptionVolatilityNumericalRules NumericalRules { get; init; } = new();
    [MessagePack.Key(14)] public VolatilityStrategyPolicy IronCondor { get; init; } = VolatilityStrategyPolicy.Optional;
    [MessagePack.Key(15)] public VolatilityStrategyPolicy VerticalSpread { get; init; } = VolatilityStrategyPolicy.Optional;
    [MessagePack.Key(16)] public VolatilityStrategyPolicy FuturesOutright { get; init; } = VolatilityStrategyPolicy.Disabled;
    [MessagePack.Key(17)] public HeldOptionVolatilityPolicy HeldOptions { get; init; } = HeldOptionVolatilityPolicy.ObserveOnly;
    [MessagePack.Key(18)] public ProtectiveActionPolicy ProtectiveClose { get; init; } = ProtectiveActionPolicy.Independent;
    [MessagePack.Key(19)] public ProtectiveActionPolicy ProtectiveCancel { get; init; } = ProtectiveActionPolicy.Independent;
}
