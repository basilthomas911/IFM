namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

public enum OptionVolatilityEnvironment : byte { Unknown = 0, Production = 1, Paper = 2, Simulation = 3 }
public enum OptionVolatilityAtmConvention : byte { Unknown = 0, Forward = 1 }
public enum OptionVolatilityOptionCombination : byte { Unknown = 0, CallPutArithmeticMean = 1 }
public enum OptionVolatilityExpirySelection : byte { Unknown = 0, QualifiedBracketingExpiries = 1 }
public enum OptionVolatilityInterpolation : byte { Unknown = 0, LinearTotalVariance = 1 }
public enum OptionVolatilityUnderlyingMatch : byte { Unknown = 0, Exact = 1 }
public enum OptionVolatilityQuoteMark : byte { Unknown = 0, ExecutableMidpoint = 1 }
public enum OptionVolatilityCrossedMarketPolicy : byte { Unknown = 0, Reject = 1 }
public enum OptionVolatilityLockedMarketPolicy : byte { Unknown = 0, Allow = 1 }
public enum OptionVolatilityExerciseStyle : byte { Unknown = 0, European = 1, American = 2 }
public enum OptionVolatilityRollPolicy : byte { Unknown = 0, StableSeries = 1 }
public enum OptionVolatilitySamplingPolicy : byte { Unknown = 0, ExchangeDefinedDailySettlement = 1 }
public enum OptionVolatilityGapPolicy : byte { Unknown = 0, Preserve = 1 }
public enum OptionVolatilityRankBounds : byte { Unknown = 0, CurrentAndPrior = 1 }
public enum OptionVolatilityPercentileConvention : byte { Unknown = 0, StrictlyBelowPrior = 1 }
public enum ImpliedVolatilityUnit : byte { Unknown = 0, AnnualDecimal = 1 }
public enum VolatilityMetricUnit : byte { Unknown = 0, ZeroToOneHundred = 1 }

/// <summary>Immutable authoring contract for a comparable option-volatility series.</summary>
public sealed record OptionVolatilitySeriesParameterSet
{
    public int SchemaVersion { get; init; } = 1;
    public Guid ParameterSetId { get; init; }
    public int Version { get; init; } = 1;
    public string SeriesId { get; init; } = "ES-ATM-30D";
    public string MethodologyVersion { get; init; } = "methodology-v1";
    public string UnderlyingRoot { get; init; } = "ES";
    public string Venue { get; init; } = "XCME";
    public string Currency { get; init; } = "USD";
    public OptionVolatilityEnvironment Environment { get; init; } = OptionVolatilityEnvironment.Production;
    public string MarketDataProvider { get; init; } = "Databento";
    public string Dataset { get; init; } = "GLBX.MDP3";
    public string[] EligibleProductFamilies { get; init; } = ["ES", "EW", "EOM"];
    public int TargetMaturityCalendarDays { get; init; } = 30;
    public OptionVolatilityAtmConvention AtmConvention { get; init; } = OptionVolatilityAtmConvention.Forward;
    public OptionVolatilityOptionCombination OptionCombination { get; init; } = OptionVolatilityOptionCombination.CallPutArithmeticMean;
    public OptionVolatilityExpirySelection ExpirySelection { get; init; } = OptionVolatilityExpirySelection.QualifiedBracketingExpiries;
    public OptionVolatilityInterpolation Interpolation { get; init; } = OptionVolatilityInterpolation.LinearTotalVariance;
    public OptionVolatilityUnderlyingMatch UnderlyingMatch { get; init; } = OptionVolatilityUnderlyingMatch.Exact;
    public OptionVolatilityQuoteMark QuoteMark { get; init; } = OptionVolatilityQuoteMark.ExecutableMidpoint;
    public int MinimumDisplayedSize { get; init; } = 1;
    public OptionVolatilityCrossedMarketPolicy CrossedMarketPolicy { get; init; } = OptionVolatilityCrossedMarketPolicy.Reject;
    public OptionVolatilityLockedMarketPolicy LockedMarketPolicy { get; init; } = OptionVolatilityLockedMarketPolicy.Allow;
    public int MaximumQuoteAgeSeconds { get; init; } = 5;
    public int MaximumIvAgeSeconds { get; init; } = 5;
    public int MaximumQuoteSkewMilliseconds { get; init; } = 250;
    public OptionVolatilityPricerSelection[] Pricers { get; init; } =
    [
        new(OptionVolatilityExerciseStyle.European, ["Black76.Managed/v1", "Black76.Rust/v1"]),
        new(OptionVolatilityExerciseStyle.American, ["AmericanFutures.CRR.Managed/v1"])
    ];
    public OptionVolatilityRollPolicy RollPolicy { get; init; } = OptionVolatilityRollPolicy.StableSeries;
    public string ExchangeCalendar { get; init; } = "CME";
    public string TimeZone { get; init; } = "America/Chicago";
    public OptionVolatilitySamplingPolicy DailySamplingPolicy { get; init; } = OptionVolatilitySamplingPolicy.ExchangeDefinedDailySettlement;
    public int IntradayCoalescingMinutes { get; init; } = 5;
    public int MaximumIntradayCheckpoints { get; init; } = 84;
    public int HistoricalLookbackSessions { get; init; } = 252;
    public int MinimumValidObservations { get; init; } = 220;
    public decimal MinimumCoverage { get; init; } = .85m;
    public OptionVolatilityGapPolicy GapPolicy { get; init; } = OptionVolatilityGapPolicy.Preserve;
    public OptionVolatilityRankBounds RankBounds { get; init; } = OptionVolatilityRankBounds.CurrentAndPrior;
    public OptionVolatilityPercentileConvention PercentileConvention { get; init; } = OptionVolatilityPercentileConvention.StrictlyBelowPrior;
    public ImpliedVolatilityUnit ImpliedVolatilityUnit { get; init; } = ImpliedVolatilityUnit.AnnualDecimal;
    public VolatilityMetricUnit RankAndPercentileUnit { get; init; } = VolatilityMetricUnit.ZeroToOneHundred;
}

public sealed record OptionVolatilityPricerSelection(
    OptionVolatilityExerciseStyle ExerciseStyle,
    string[] PricerVersions);

public enum VolatilityEvidenceRequirement : byte { Unknown = 0, Optional = 1, Required = 2 }
public enum ClosedMarketEvidencePolicy : byte { Unknown = 0, LatestQualifiedSession = 1 }
public enum UnavailableVolatilityPolicy : byte { Unknown = 0, NeverTreatAsZero = 1 }
public enum VolatilityStrategyPolicy : byte { Unknown = 0, Disabled = 1, Optional = 2, Required = 3 }
public enum HeldOptionVolatilityPolicy : byte { Unknown = 0, ObserveOnly = 1 }
public enum ProtectiveActionPolicy : byte { Unknown = 0, Independent = 1 }

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

public sealed record OptionVolatilityNumericalRules
{
    public decimal? MinimumIvRank { get; init; }
    public decimal? MaximumIvRank { get; init; }
    public decimal? MinimumIvPercentile { get; init; }
    public decimal? MaximumIvPercentile { get; init; }
    public decimal? DeltaTargetAdjustment { get; init; }
    public decimal? SpreadWidthAdjustment { get; init; }
    public decimal? RiskLimitAdjustment { get; init; }
}

public enum RetentionDependencyPolicy : byte { Unknown = 0, FollowSnapshotCannotExpireIndependently = 1 }
public enum RetentionLifetime : byte { Unknown = 0, Permanent = 1 }
public enum CorrectionRetentionPolicy : byte { Unknown = 0, Retain = 1 }
public enum RetentionBucket : byte { Unknown = 0, Monthly = 1 }
public enum HistoricalVolatilitySource : byte { Unknown = 0, DatabentoQuoteHistory = 1, IfmReferenceMetadata = 2 }
public enum TradeIvBackfillPolicy : byte { Unknown = 0, Disallowed = 1 }

/// <summary>Immutable authoring contract for option-volatility history and evidence retention.</summary>
public sealed record OptionVolatilityRetentionParameterSet
{
    public int SchemaVersion { get; init; } = 1;
    public Guid ParameterSetId { get; init; }
    public int Version { get; init; } = 1;
    public int SourceObservationYears { get; init; } = 2;
    public int FinalizedDailyMetricYears { get; init; } = 7;
    public int IntradayCheckpointDays { get; init; } = 90;
    public int DecisionSnapshotYearsAfterCompletion { get; init; } = 7;
    public RetentionDependencyPolicy ReferencedEvidencePolicy { get; init; } = RetentionDependencyPolicy.FollowSnapshotCannotExpireIndependently;
    public RetentionLifetime LatestPointerLifetime { get; init; } = RetentionLifetime.Permanent;
    public RetentionLifetime DefinitionLifetime { get; init; } = RetentionLifetime.Permanent;
    public RetentionLifetime PublishedParameterLifetime { get; init; } = RetentionLifetime.Permanent;
    public CorrectionRetentionPolicy CorrectionPolicy { get; init; } = CorrectionRetentionPolicy.Retain;
    public int BackfillStagingDays { get; init; } = 30;
    public int FailedPublicationDays { get; init; } = 30;
    public RetentionBucket PartitionBucket { get; init; } = RetentionBucket.Monthly;
    public int MaximumPageSize { get; init; } = 256;
    public HistoricalVolatilitySource[] HistoricalSources { get; init; } =
        [HistoricalVolatilitySource.DatabentoQuoteHistory, HistoricalVolatilitySource.IfmReferenceMetadata];
    public TradeIvBackfillPolicy TradeIvBackfill { get; init; } = TradeIvBackfillPolicy.Disallowed;
    public bool AllowUnqualifiedBackfillPublication { get; init; }
    public OptionVolatilityEnvironment[] IsolatedEnvironments { get; init; } =
        [OptionVolatilityEnvironment.Production, OptionVolatilityEnvironment.Paper, OptionVolatilityEnvironment.Simulation];
}
