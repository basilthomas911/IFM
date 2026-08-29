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
    DayOfWeek[]? _eligibleWeekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    [Key(0)] public string ExchangeTimeZoneId { get; init; } = "America/New_York";
    [Key(1)] public DayOfWeek[] EligibleWeekdays
    {
        get => _eligibleWeekdays is null ? null! : [.. _eligibleWeekdays];
        init => _eligibleWeekdays = value is null ? null : [.. value.Order()];
    }
    [Key(2)] public TimeSpan EntryWindowStart { get; init; } = new(9, 35, 0);
    [Key(3)] public TimeSpan EntryWindowEnd { get; init; } = new(15, 30, 0);
    [Key(4)] public bool RequireOpenExchangeState { get; init; } = true;
}

[MessagePackObject]
public sealed record MarketConditionEventRiskConfiguration
{
    string[]? _requiredEventCategories = ["HighImpact", "RateDecision"];

    [Key(0)] public int HighImpactBeforeMinutes { get; init; } = 15;
    [Key(1)] public int HighImpactAfterMinutes { get; init; } = 10;
    [Key(2)] public int RateDecisionBeforeMinutes { get; init; } = 30;
    [Key(3)] public int RateDecisionAfterMinutes { get; init; } = 20;
    [Key(4)] public string[] RequiredEventCategories
    {
        get => _requiredEventCategories is null ? null! : [.. _requiredEventCategories];
        init => _requiredEventCategories = value is null
            ? null
            : [.. value.Order(StringComparer.Ordinal)];
    }
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
    string[]? _requiredHealthSources =
        ["FuturesOptionFeed", "IbkrSession", "LatestValueCache", "PrimaryFuturesFeed"];

    [Key(0)] public string[] RequiredHealthSources
    {
        get => _requiredHealthSources is null ? null! : [.. _requiredHealthSources];
        init => _requiredHealthSources = value is null
            ? null
            : [.. value.Order(StringComparer.Ordinal)];
    }
    [Key(1)] public bool TreatReportedDegradedAsBlocked { get; init; } = true;
}

[MessagePackObject]
public sealed record MarketConditionWorkflowEligibilityConfiguration
{
    RegimeRestriction[]? _blockingRegimeRestrictions = [RegimeRestriction.NoNewTrade];

    [Key(0)] public int MaximumRegimeAgeSeconds { get; init; } = 120;
    [Key(1)] public int MaximumTriggerAgeSeconds { get; init; } = 30;
    [Key(2)] public bool RequireEntriesEnabled { get; init; } = true;
    [Key(3)] public RegimeRestriction[] BlockingRegimeRestrictions
    {
        get => _blockingRegimeRestrictions is null ? null! : [.. _blockingRegimeRestrictions];
        init => _blockingRegimeRestrictions = value is null ? null : [.. value.Order()];
    }
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
            RuleFor(x => x.Snapshot).NotNull().SetValidator(new SnapshotValidator());
            RuleFor(x => x.Session).NotNull().SetValidator(new SessionValidator());
            RuleFor(x => x.EventRisk).NotNull().SetValidator(new EventRiskValidator());
            RuleFor(x => x.MarketIntegrity).NotNull().SetValidator(new MarketIntegrityValidator());
            RuleFor(x => x.FuturesLiquidity).NotNull().SetValidator(new FuturesLiquidityValidator());
            RuleFor(x => x.OptionLiquidity).NotNull().SetValidator(new OptionLiquidityValidator());
            RuleFor(x => x.OperationalReadiness).NotNull().SetValidator(new OperationalReadinessValidator());
            RuleFor(x => x.WorkflowEligibility).NotNull().SetValidator(new WorkflowEligibilityValidator());
            RuleFor(x => x.Classification).NotNull().SetValidator(new ClassificationValidator());
            RuleFor(x => x.Scoring).NotNull().SetValidator(new ScoringValidator());
            RuleFor(x => x.Execution).NotNull().SetValidator(new ExecutionValidator());
            RuleFor(x => x.SummaryTemplateVersion).GreaterThan((ushort)0);
        }
    }

    sealed class SnapshotValidator : AbstractValidator<MarketConditionSnapshotConfiguration>
    {
        public SnapshotValidator()
        {
            RuleFor(x => x.FutureClockSkewSeconds).GreaterThanOrEqualTo(0);
            RuleFor(x => x.SnapshotCaptureAttempts).InclusiveBetween(1, 10);
            RuleFor(x => x.FuturesQuoteMaximumAgeSeconds).GreaterThan(0);
            RuleFor(x => x.FuturesTradeMaximumAgeSeconds).GreaterThan(0);
            RuleFor(x => x.OptionQuoteMaximumAgeSeconds).GreaterThan(0);
            RuleFor(x => x.OptionChainMaximumAgeSeconds).GreaterThan(0);
            RuleFor(x => x.VolatilityMaximumAgeSeconds).GreaterThan(0);
            RuleFor(x => x.SessionMaximumAgeSeconds).GreaterThan(0);
            RuleFor(x => x.HealthMaximumAgeSeconds).GreaterThan(0);
            RuleFor(x => x.EventRiskMaximumAgeSeconds).GreaterThan(0);
        }
    }

    sealed class SessionValidator : AbstractValidator<MarketConditionSessionConfiguration>
    {
        public SessionValidator()
        {
            RuleFor(x => x.ExchangeTimeZoneId).NotEmpty();
            RuleFor(x => x.EligibleWeekdays).NotNull().NotEmpty()
                .Must(Unique).WithMessage("Eligible weekdays must be unique.");
            RuleForEach(x => x.EligibleWeekdays).IsInEnum();
            RuleFor(x => x.EntryWindowStart).GreaterThanOrEqualTo(TimeSpan.Zero).LessThan(TimeSpan.FromDays(1));
            RuleFor(x => x.EntryWindowEnd).GreaterThan(TimeSpan.Zero).LessThanOrEqualTo(TimeSpan.FromDays(1));
            RuleFor(x => x).Must(x => x.EntryWindowStart < x.EntryWindowEnd)
                .WithMessage("Entry window start must precede entry window end.");
        }

        static bool Unique(DayOfWeek[]? values) => values is not null && values.Distinct().Count() == values.Length;
    }

    sealed class EventRiskValidator : AbstractValidator<MarketConditionEventRiskConfiguration>
    {
        public EventRiskValidator()
        {
            RuleFor(x => x.HighImpactBeforeMinutes).GreaterThan(0);
            RuleFor(x => x.HighImpactAfterMinutes).GreaterThan(0);
            RuleFor(x => x.RateDecisionBeforeMinutes).GreaterThan(0);
            RuleFor(x => x.RateDecisionAfterMinutes).GreaterThan(0);
            RuleFor(x => x.RequiredEventCategories).NotNull().NotEmpty()
                .Must(UniqueNonBlank).WithMessage("Required event categories must be non-blank and unique.");
            RuleForEach(x => x.RequiredEventCategories).NotEmpty();
        }
    }

    sealed class MarketIntegrityValidator : AbstractValidator<MarketConditionMarketIntegrityConfiguration>
    {
        public MarketIntegrityValidator()
        {
            RuleFor(x => x.MaximumOneMinuteMoveAtr).GreaterThan(0m);
            RuleFor(x => x.MaximumFiveMinuteVolatilityIncrease).InclusiveBetween(0m, 1m);
        }
    }

    sealed class FuturesLiquidityValidator : AbstractValidator<MarketConditionFuturesLiquidityConfiguration>
    {
        public FuturesLiquidityValidator()
        {
            RuleFor(x => x.TickSize).GreaterThan(0m);
            RuleFor(x => x.HealthySpreadTicks).GreaterThan(0m);
            RuleFor(x => x.MaximumTradeableSpreadTicks).GreaterThan(1m);
            RuleFor(x => x).Must(x => x.HealthySpreadTicks <= x.MaximumTradeableSpreadTicks)
                .WithMessage("Healthy futures spread must not exceed the maximum tradeable spread.");
            RuleFor(x => x.MinimumBidSize).GreaterThan(0m);
            RuleFor(x => x.MinimumAskSize).GreaterThan(0m);
            RuleFor(x => x.HealthyBestSideSize).GreaterThan(0m);
        }
    }

    sealed class OptionLiquidityValidator : AbstractValidator<MarketConditionOptionLiquidityConfiguration>
    {
        public OptionLiquidityValidator()
        {
            RuleFor(x => x.MinimumDte).GreaterThanOrEqualTo(0);
            RuleFor(x => x.MaximumDte).GreaterThanOrEqualTo(0);
            RuleFor(x => x).Must(x => x.MinimumDte <= x.MaximumDte)
                .WithMessage("Minimum DTE must not exceed maximum DTE.");
            RuleFor(x => x.MaximumAbsoluteMoneyness).InclusiveBetween(0m, 1m);
            RuleFor(x => x.MinimumEligibleExpirations).GreaterThan(0);
            RuleFor(x => x.MinimumCandidateContracts).GreaterThan(0);
            RuleFor(x => x.MinimumValidQuoteCoverage).InclusiveBetween(0m, 1m);
            RuleFor(x => x.HealthyValidQuoteCoverage).ExclusiveBetween(0m, 1.000001m);
            RuleFor(x => x).Must(x => x.MinimumValidQuoteCoverage <= x.HealthyValidQuoteCoverage)
                .WithMessage("Minimum quote coverage must not exceed healthy quote coverage.");
            RuleFor(x => x.MaximumMedianRelativeSpread).ExclusiveBetween(0m, 1.000001m);
            RuleFor(x => x.MaximumP90RelativeSpread).InclusiveBetween(0m, 1m);
            RuleFor(x => x).Must(x => x.MaximumMedianRelativeSpread <= x.MaximumP90RelativeSpread)
                .WithMessage("Median relative spread must not exceed P90 relative spread.");
            RuleFor(x => x.MinimumMedianBidSize).GreaterThan(0m);
            RuleFor(x => x.MinimumMedianAskSize).GreaterThan(0m);
            RuleFor(x => x.MaximumUnderlyingMismatch).InclusiveBetween(0m, 1m);
        }
    }

    sealed class OperationalReadinessValidator : AbstractValidator<MarketConditionOperationalReadinessConfiguration>
    {
        public OperationalReadinessValidator()
        {
            RuleFor(x => x.RequiredHealthSources).NotNull().NotEmpty()
                .Must(UniqueNonBlank).WithMessage("Required health sources must be non-blank and unique.");
            RuleForEach(x => x.RequiredHealthSources).NotEmpty();
        }
    }

    sealed class WorkflowEligibilityValidator : AbstractValidator<MarketConditionWorkflowEligibilityConfiguration>
    {
        public WorkflowEligibilityValidator()
        {
            RuleFor(x => x.MaximumRegimeAgeSeconds).GreaterThan(0);
            RuleFor(x => x.MaximumTriggerAgeSeconds).GreaterThan(0);
            RuleFor(x => x.BlockingRegimeRestrictions).NotNull().NotEmpty()
                .Must(Unique).WithMessage("Blocking regime restrictions must be unique.");
            RuleForEach(x => x.BlockingRegimeRestrictions).IsInEnum();
        }

        static bool Unique(RegimeRestriction[]? values) =>
            values is not null && values.Distinct().Count() == values.Length;
    }

    sealed class ClassificationValidator : AbstractValidator<MarketConditionClassificationConfiguration>
    {
        public ClassificationValidator()
        {
            RuleFor(x => x.WeakeningReversalLevel).InclusiveBetween(0m, 1m);
            RuleFor(x => x.ExhaustingReversalLevel).InclusiveBetween(0m, 1m);
            RuleFor(x => x).Must(x => x.WeakeningReversalLevel <= x.ExhaustingReversalLevel)
                .WithMessage("Weakening reversal level must not exceed exhausting reversal level.");
            RuleFor(x => x.ConfirmedBandLevel).GreaterThan(0m);
            RuleFor(x => x.HealthyLiquidityScore).InclusiveBetween(0m, 1m);
            RuleFor(x => x.HealthyDataQualityScore).InclusiveBetween(0m, 1m);
        }
    }

    sealed class ScoringValidator : AbstractValidator<MarketConditionScoringConfiguration>
    {
        public ScoringValidator()
        {
            RuleFor(x => x.RegimeAlignmentWeight).InclusiveBetween(0m, 1m);
            RuleFor(x => x.TriggerQualityWeight).InclusiveBetween(0m, 1m);
            RuleFor(x => x.FuturesLiquidityWeight).InclusiveBetween(0m, 1m);
            RuleFor(x => x.OptionLiquidityWeight).InclusiveBetween(0m, 1m);
            RuleFor(x => x.DataQualityWeight).InclusiveBetween(0m, 1m);
            RuleFor(x => x.EntryTimingWeight).InclusiveBetween(0m, 1m);
            RuleFor(x => Weight(x)).Must(IsUnitWeight).WithMessage("Scoring weights must sum to one.");
            RuleFor(x => x.MinimumStrength).InclusiveBetween(0m, 100m);
            RuleFor(x => x.MinimumConfidence).InclusiveBetween(0m, 1m);
            RuleFor(x => x.OptionalMissingPenalty).InclusiveBetween(0m, 1m);
            RuleFor(x => x.OptionalMissingMaximumPenalty).InclusiveBetween(0m, 1m);
            RuleFor(x => x).Must(x => x.OptionalMissingPenalty <= x.OptionalMissingMaximumPenalty)
                .WithMessage("Optional missing penalty must not exceed its maximum.");
            RuleFor(x => x.LowConfidencePenalty).InclusiveBetween(0m, 1m);
            RuleFor(x => x.TransitionPenalty).InclusiveBetween(0m, 1m);
            RuleFor(x => x.ConflictingEvidencePenalty).InclusiveBetween(0m, 1m);
            RuleFor(x => x.ConflictingEvidenceMaximumPenalty).InclusiveBetween(0m, 1m);
            RuleFor(x => x).Must(x => x.ConflictingEvidencePenalty <= x.ConflictingEvidenceMaximumPenalty)
                .WithMessage("Conflicting evidence penalty must not exceed its maximum.");
            RuleFor(x => x.MaximumTotalPenalty).InclusiveBetween(0m, 1m);
        }

        static decimal Weight(MarketConditionScoringConfiguration x) => x.RegimeAlignmentWeight +
            x.TriggerQualityWeight + x.FuturesLiquidityWeight + x.OptionLiquidityWeight +
            x.DataQualityWeight + x.EntryTimingWeight;
        static bool IsUnitWeight(decimal value) => Math.Abs(value - 1m) <= 0.000001m;
    }

    sealed class ExecutionValidator : AbstractValidator<MarketConditionExecutionConfiguration>
    {
        public ExecutionValidator()
        {
            RuleFor(x => x.MaximumExecutionMilliseconds).GreaterThan(0);
            RuleFor(x => x.TransportReplyGraceMilliseconds).GreaterThan(0);
            RuleFor(x => x.ResultLifetimeSeconds).GreaterThan(0);
        }
    }

    static bool UniqueNonBlank(string[]? values) => values is not null &&
        values.All(static value => !string.IsNullOrWhiteSpace(value)) &&
        values.Distinct(StringComparer.Ordinal).Count() == values.Length;
}
