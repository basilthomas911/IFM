using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;

[MessagePackObject]
public sealed record MarketConditionSnapshotConfiguration
{
    [Key(0)] public int FutureClockSkewSeconds { get; init; } = 2;
    [Key(1)] public int SnapshotCaptureAttempts { get; init; } = 3;
    [Key(2)] public int FuturesQuoteMaximumAgeSeconds { get; init; } = 2;
    [Key(3)] public int FuturesTradeMaximumAgeSeconds { get; init; } = 5;
    [Key(4)] public int OptionQuoteMaximumAgeSeconds { get; init; } = 5;
    [Key(5)] public int OptionChainMaximumAgeSeconds { get; init; } = 10;
    [Key(6)] public int VolatilityMaximumAgeSeconds { get; init; } = 15;
    [Key(7)] public int SessionMaximumAgeSeconds { get; init; } = 60;
    [Key(8)] public int HealthMaximumAgeSeconds { get; init; } = 15;
    [Key(9)] public int EventRiskMaximumAgeSeconds { get; init; } = 900;
}

[MessagePackObject]
public sealed record MarketConditionSessionConfiguration
{
    [Key(0)] public string ExchangeTimeZoneId { get; init; } = "America/New_York";
    [Key(1)] public DayOfWeek[] EligibleWeekdays { get; init; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];
    [Key(2)] public TimeSpan EntryWindowStart { get; init; } = new(9, 35, 0);
    [Key(3)] public TimeSpan EntryWindowEnd { get; init; } = new(15, 30, 0);
    [Key(4)] public bool RequireOpenExchangeState { get; init; } = true;
}

[MessagePackObject]
public sealed record MarketConditionEventRiskConfiguration
{
    [Key(0)] public int HighImpactBeforeMinutes { get; init; } = 15;
    [Key(1)] public int HighImpactAfterMinutes { get; init; } = 10;
    [Key(2)] public int RateDecisionBeforeMinutes { get; init; } = 30;
    [Key(3)] public int RateDecisionAfterMinutes { get; init; } = 20;
    [Key(4)] public string[] RequiredEventCategories { get; init; } = ["HighImpact", "RateDecision"];
}

[MessagePackObject]
public sealed record MarketConditionMarketIntegrityConfiguration
{
    [Key(0)] public decimal MaximumOneMinuteMoveAtr { get; init; } = 1.50m;
    [Key(1)] public decimal MaximumFiveMinuteVolatilityIncrease { get; init; } = 0.15m;
    [Key(2)] public bool PermitCrossedMarket { get; init; }
    [Key(3)] public bool RequirePositiveTwoSidedQuote { get; init; } = true;
}

[MessagePackObject]
public sealed record MarketConditionFuturesLiquidityConfiguration
{
    [Key(0)] public decimal TickSize { get; init; } = 0.25m;
    [Key(1)] public decimal HealthySpreadTicks { get; init; } = 1m;
    [Key(2)] public decimal MaximumTradeableSpreadTicks { get; init; } = 2m;
    [Key(3)] public decimal MinimumBidSize { get; init; } = 5m;
    [Key(4)] public decimal MinimumAskSize { get; init; } = 5m;
    [Key(5)] public decimal HealthyBestSideSize { get; init; } = 10m;
}

[MessagePackObject]
public sealed record MarketConditionOptionLiquidityConfiguration
{
    [Key(0)] public int MinimumDte { get; init; } = 1;
    [Key(1)] public int MaximumDte { get; init; } = 14;
    [Key(2)] public decimal MaximumAbsoluteMoneyness { get; init; } = 0.05m;
    [Key(3)] public bool RequireCallsAndPuts { get; init; } = true;
    [Key(4)] public int MinimumEligibleExpirations { get; init; } = 1;
    [Key(5)] public int MinimumCandidateContracts { get; init; } = 12;
    [Key(6)] public decimal MinimumValidQuoteCoverage { get; init; } = 0.80m;
    [Key(7)] public decimal HealthyValidQuoteCoverage { get; init; } = 0.90m;
    [Key(8)] public decimal MaximumMedianRelativeSpread { get; init; } = 0.20m;
    [Key(9)] public decimal MaximumP90RelativeSpread { get; init; } = 0.35m;
    [Key(10)] public decimal MinimumMedianBidSize { get; init; } = 1m;
    [Key(11)] public decimal MinimumMedianAskSize { get; init; } = 1m;
    [Key(12)] public decimal MaximumUnderlyingMismatch { get; init; } = 0.0025m;
}

[MessagePackObject]
public sealed record MarketConditionOperationalReadinessConfiguration
{
    [Key(0)] public string[] RequiredHealthSources { get; init; } =
        ["PrimaryFuturesFeed", "FuturesOptionFeed", "LatestValueCache", "IbkrSession"];
    [Key(1)] public bool TreatReportedDegradedAsBlocked { get; init; } = true;
}

[MessagePackObject]
public sealed record MarketConditionWorkflowEligibilityConfiguration
{
    [Key(0)] public int MaximumRegimeAgeSeconds { get; init; } = 120;
    [Key(1)] public int MaximumTriggerAgeSeconds { get; init; } = 30;
    [Key(2)] public bool RequireEntriesEnabled { get; init; } = true;
    [Key(3)] public RegimeRestriction[] BlockingRegimeRestrictions { get; init; } = [RegimeRestriction.NoNewTrade];
}

[MessagePackObject]
public sealed record MarketConditionClassificationConfiguration
{
    [Key(0)] public decimal WeakeningReversalLevel { get; init; } = 0.40m;
    [Key(1)] public decimal ExhaustingReversalLevel { get; init; } = 0.70m;
    [Key(2)] public decimal ConfirmedBandLevel { get; init; } = 1m;
    [Key(3)] public decimal HealthyLiquidityScore { get; init; } = 0.75m;
    [Key(4)] public decimal HealthyDataQualityScore { get; init; } = 0.75m;
}

[MessagePackObject]
public sealed record MarketConditionScoringConfiguration
{
    [Key(0)] public decimal RegimeAlignmentWeight { get; init; } = 0.30m;
    [Key(1)] public decimal TriggerQualityWeight { get; init; } = 0.25m;
    [Key(2)] public decimal FuturesLiquidityWeight { get; init; } = 0.15m;
    [Key(3)] public decimal OptionLiquidityWeight { get; init; } = 0.15m;
    [Key(4)] public decimal DataQualityWeight { get; init; } = 0.10m;
    [Key(5)] public decimal EntryTimingWeight { get; init; } = 0.05m;
    [Key(6)] public decimal MinimumStrength { get; init; } = 55m;
    [Key(7)] public decimal MinimumConfidence { get; init; } = 0.65m;
    [Key(8)] public decimal OptionalMissingPenalty { get; init; } = 0.05m;
    [Key(9)] public decimal OptionalMissingMaximumPenalty { get; init; } = 0.15m;
    [Key(10)] public decimal LowConfidencePenalty { get; init; } = 0.10m;
    [Key(11)] public decimal TransitionPenalty { get; init; } = 0.10m;
    [Key(12)] public decimal ConflictingEvidencePenalty { get; init; } = 0.10m;
    [Key(13)] public decimal ConflictingEvidenceMaximumPenalty { get; init; } = 0.20m;
    [Key(14)] public decimal MaximumTotalPenalty { get; init; } = 0.35m;
}

[MessagePackObject]
public sealed record MarketConditionExecutionConfiguration
{
    [Key(0)] public int MaximumExecutionMilliseconds { get; init; } = 5000;
    [Key(1)] public int TransportReplyGraceMilliseconds { get; init; } = 5000;
    [Key(2)] public int ResultLifetimeSeconds { get; init; } = 30;
}

/// <summary>Complete immutable V1 Market Condition configuration.</summary>
[MessagePackObject]
public sealed record MarketConditionParameterSet
{
    public const ushort CurrentSchemaVersion = 1;
    [Key(0)] public Guid ParameterSetId { get; init; }
    [Key(1)] public int Version { get; init; }
    [Key(2)] public ushort SchemaVersion { get; init; } = CurrentSchemaVersion;
    [Key(3)] public Guid StrategyParameterSetId { get; init; }
    [Key(4)] public int StrategyParameterSetVersion { get; init; }
    [Key(5)] public int FundId { get; init; }
    [Key(6)] public string InstrumentRoot { get; init; } = "ES";
    [Key(7)] public TimeFrameType TargetHorizon { get; init; }
    [Key(8)] public MarketConditionSnapshotConfiguration Snapshot { get; init; } = new();
    [Key(9)] public MarketConditionSessionConfiguration Session { get; init; } = new();
    [Key(10)] public MarketConditionEventRiskConfiguration EventRisk { get; init; } = new();
    [Key(11)] public MarketConditionMarketIntegrityConfiguration MarketIntegrity { get; init; } = new();
    [Key(12)] public MarketConditionFuturesLiquidityConfiguration FuturesLiquidity { get; init; } = new();
    [Key(13)] public MarketConditionOptionLiquidityConfiguration OptionLiquidity { get; init; } = new();
    [Key(14)] public MarketConditionOperationalReadinessConfiguration OperationalReadiness { get; init; } = new();
    [Key(15)] public MarketConditionWorkflowEligibilityConfiguration WorkflowEligibility { get; init; } = new();
    [Key(16)] public MarketConditionClassificationConfiguration Classification { get; init; } = new();
    [Key(17)] public MarketConditionScoringConfiguration Scoring { get; init; } = new();
    [Key(18)] public MarketConditionExecutionConfiguration Execution { get; init; } = new();
    [Key(19)] public ushort SummaryTemplateVersion { get; init; } = 1;

    public static MarketConditionParameterSet CreateDefault(
        Guid parameterSetId,
        Guid strategyParameterSetId,
        int fundId,
        TimeFrameType targetHorizon,
        int version = 1,
        int strategyVersion = 1)
    {
        var horizon = targetHorizon switch
        {
            TimeFrameType.Daily => (1, 14, 55m, 0.65m, 30),
            TimeFrameType.Weekly => (7, 45, 60m, 0.68m, 60),
            TimeFrameType.Monthly => (21, 90, 65m, 0.70m, 90),
            _ => throw new ArgumentOutOfRangeException(nameof(targetHorizon))
        };
        return new MarketConditionParameterSet
        {
            ParameterSetId = parameterSetId,
            Version = version,
            StrategyParameterSetId = strategyParameterSetId,
            StrategyParameterSetVersion = strategyVersion,
            FundId = fundId,
            TargetHorizon = targetHorizon,
            OptionLiquidity = new MarketConditionOptionLiquidityConfiguration
                { MinimumDte = horizon.Item1, MaximumDte = horizon.Item2 },
            Scoring = new MarketConditionScoringConfiguration
                { MinimumStrength = horizon.Item3, MinimumConfidence = horizon.Item4 },
            Execution = new MarketConditionExecutionConfiguration { ResultLifetimeSeconds = horizon.Item5 }
        };
    }
}

public sealed class MarketConditionParameterSetValidationRules
    : BaseValidationRules, IValidationRules<MarketConditionParameterSet>
{
    static readonly Validator Rules = new();
    public ValidationError[] Execute(MarketConditionParameterSet value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<MarketConditionParameterSet>
    {
        public Validator()
        {
            RuleFor(x => x.ParameterSetId).NotEmpty();
            RuleFor(x => x.Version).GreaterThan(0);
            RuleFor(x => x.SchemaVersion).Equal(MarketConditionParameterSet.CurrentSchemaVersion);
            RuleFor(x => x.StrategyParameterSetId).NotEmpty();
            RuleFor(x => x.StrategyParameterSetVersion).GreaterThan(0);
            RuleFor(x => x.FundId).GreaterThan(0);
            RuleFor(x => x.InstrumentRoot).Equal("ES");
            RuleFor(x => x.TargetHorizon).Must(static x => x is TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly);
            RuleFor(x => x.Snapshot.SnapshotCaptureAttempts).InclusiveBetween(1, 10);
            RuleFor(x => x.Snapshot.FutureClockSkewSeconds).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Session.EligibleWeekdays).NotEmpty();
            RuleFor(x => x.Session.ExchangeTimeZoneId).NotEmpty();
            RuleFor(x => x).Must(x => x.Session.EntryWindowStart < x.Session.EntryWindowEnd)
                .WithMessage("Entry window start must precede entry window end.");
            RuleFor(x => x.EventRisk.RequiredEventCategories).NotEmpty();
            RuleFor(x => x.OperationalReadiness.RequiredHealthSources).NotEmpty();
            RuleFor(x => x.OptionLiquidity.MinimumDte).GreaterThanOrEqualTo(0);
            RuleFor(x => x).Must(x => x.OptionLiquidity.MinimumDte <= x.OptionLiquidity.MaximumDte)
                .WithMessage("Minimum DTE must not exceed maximum DTE.");
            RuleFor(x => Weight(x.Scoring)).Must(IsUnitWeight).WithMessage("Scoring weights must sum to one.");
            RuleFor(x => x.Scoring.MinimumStrength).InclusiveBetween(0m, 100m);
            RuleFor(x => x.Scoring.MinimumConfidence).InclusiveBetween(0m, 1m);
            RuleFor(x => x.Execution.MaximumExecutionMilliseconds).GreaterThan(0);
            RuleFor(x => x.Execution.TransportReplyGraceMilliseconds).GreaterThan(0);
            RuleFor(x => x.Execution.ResultLifetimeSeconds).GreaterThan(0);
            RuleFor(x => x.SummaryTemplateVersion).GreaterThan((ushort)0);
        }

        static decimal Weight(MarketConditionScoringConfiguration x) => x.RegimeAlignmentWeight +
            x.TriggerQualityWeight + x.FuturesLiquidityWeight + x.OptionLiquidityWeight +
            x.DataQualityWeight + x.EntryTimingWeight;
        static bool IsUnitWeight(decimal value) => Math.Abs(value - 1m) <= 0.000001m;
    }
}
