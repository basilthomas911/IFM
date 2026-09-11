using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

/// <summary>Configures freshness validation for one immutable signal snapshot.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryFreshnessConfiguration
{
    /// <summary>Gets the maximum tolerated future clock skew in seconds.</summary>
    [Key(0)] public int FutureClockSkewSeconds { get; init; } = 5;
}

/// <summary>Configures snapshot compatibility and capture quality.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryDataQualityConfiguration
{
    /// <summary>Gets supported upstream signal schema versions.</summary>
    [Key(0)] public ushort[] SupportedSignalSchemaVersions { get; init; } = [1];
    /// <summary>Gets approved upstream calculation versions.</summary>
    [Key(1)] public string[] ApprovedCalculationVersions { get; init; } = ["1"];
    /// <summary>Gets the bounded number of revision-stable snapshot capture attempts.</summary>
    [Key(2)] public int SnapshotCaptureAttempts { get; init; } = 3;
}

/// <summary>Contains the complete immutable V1 configuration for one Regime Discovery execution.</summary>
[MessagePackObject]
public sealed record RegimeDiscoveryParameterSet
{
    /// <summary>Gets the current typed parameter schema version.</summary>
    public const ushort CurrentSchemaVersion = 1;
    /// <summary>Gets the immutable parameter-set identity.</summary>
    [Key(0)] public Guid ParameterSetId { get; init; }
    /// <summary>Gets the positive parameter-set version.</summary>
    [Key(1)] public int Version { get; init; }
    /// <summary>Gets the typed parameter schema version.</summary>
    [Key(2)] public ushort SchemaVersion { get; init; } = CurrentSchemaVersion;
    /// <summary>Gets the owning strategy parameter-set identity.</summary>
    [Key(3)] public Guid StrategyParameterSetId { get; init; }
    /// <summary>Gets the owning strategy parameter-set version.</summary>
    [Key(4)] public int StrategyParameterSetVersion { get; init; }
    /// <summary>Gets the single workflow target horizon.</summary>
    [Key(5)] public TimeFrameType TargetHorizon { get; init; }
    /// <summary>Gets the observation-timeframe mapping.</summary>
    [Key(6)] public RegimeDiscoveryHorizonConfiguration Horizon { get; init; } = new();
    /// <summary>Gets deterministic Trend parameters.</summary>
    [Key(7)] public TrendRegimeConfiguration Trend { get; init; } = new();
    /// <summary>Gets deterministic Volatility parameters.</summary>
    [Key(8)] public VolatilityRegimeConfiguration Volatility { get; init; } = new();
    /// <summary>Gets deterministic Market Structure parameters.</summary>
    [Key(9)] public MarketStructureRegimeConfiguration MarketStructure { get; init; } = new();
    /// <summary>Gets deterministic Fusion parameters.</summary>
    [Key(10)] public MarketRegimeFusionConfiguration Fusion { get; init; } = new();
    /// <summary>Gets signal freshness parameters.</summary>
    [Key(11)] public RegimeDiscoveryFreshnessConfiguration Freshness { get; init; } = new();
    /// <summary>Gets data-quality and compatibility parameters.</summary>
    [Key(12)] public RegimeDiscoveryDataQualityConfiguration DataQuality { get; init; } = new();

    /// <summary>Explicit signal list for schemas 2+; null retains the unchanged legacy schema-1 payload.</summary>
    [Key(13)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public RegimeDiscoverySignalConfiguration[]? SignalRequirements { get; init; }
    /// <summary>Gets normalized timeframe-and-period signal selections for schemas 5+.</summary>
    [Key(14)]
    [ParameterSchemaSince(5)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public RegimeDiscoverySignalMetricConfiguration[]? SignalMetrics { get; init; }

    /// <summary>Gets current market observation selections for schemas 5+.</summary>
    [Key(15)]
    [ParameterSchemaSince(5)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public RegimeDiscoveryObservationMetricConfiguration[]? ObservationMetrics { get; init; }

    /// <summary>Creates the approved V1 defaults for one target horizon.</summary>
    /// <param name="parameterSetId">Immutable Regime Discovery parameter identity.</param>
    /// <param name="strategyParameterSetId">Owning strategy parameter identity.</param>
    /// <param name="targetHorizon">Daily, Weekly, or Monthly target horizon.</param>
    /// <param name="version">Positive parameter-set version.</param>
    /// <param name="strategyVersion">Positive strategy parameter-set version.</param>
    /// <returns>A complete immutable default parameter set.</returns>
    public static RegimeDiscoveryParameterSet CreateDefault(
        Guid parameterSetId,
        Guid strategyParameterSetId,
        TimeFrameType targetHorizon,
        int version = 1,
        int strategyVersion = 1) => new()
        {
            ParameterSetId = parameterSetId,
            Version = version,
            StrategyParameterSetId = strategyParameterSetId,
            StrategyParameterSetVersion = strategyVersion,
            TargetHorizon = targetHorizon,
            Horizon = RegimeDiscoveryHorizonConfiguration.CreateDefault(targetHorizon)
        };
}

/// <summary>Validates a complete immutable Regime Discovery parameter set.</summary>
public sealed class RegimeDiscoveryParameterSetValidationRules
    : BaseValidationRules, IValidationRules<RegimeDiscoveryParameterSet>
{
    static readonly Validator Rules = new();

    /// <summary>Validates the supplied parameter set.</summary>
    /// <param name="value">Parameter set to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(RegimeDiscoveryParameterSet value) => value is null
        ? [new ValidationError("ParameterSet is required.")]
        : Validate(value, Rules);

    sealed class Validator : AbstractValidator<RegimeDiscoveryParameterSet>
    {
        public Validator()
        {
            RuleFor(x => x.ParameterSetId).NotEmpty();
            RuleFor(x => x.Version).GreaterThan(0);
            RuleFor(x => x.SchemaVersion).Must(version => version is 1 or 2 or 3 or 4 or 5);
            RuleFor(x => x).Must(x => x.SchemaVersion == 1 ? x.SignalRequirements is null : x.SignalRequirements is { Length: > 0 and <= 512 })
                .WithMessage("Schema 1 uses legacy requirements; schemas 2 through 4 require an explicit signal list.");
            RuleFor(x => x).Must(x => x.SchemaVersion < 3 ||
                (x.SignalRequirements is not null && x.SignalRequirements.All(row => row is not null && row.IsRequired)))
                .WithMessage("Schemas 3 and 4 use membership only: every signal row must be required when included.");
            RuleFor(x => x).Must(x => x.SchemaVersion < 3 ||
                (x.Horizon?.TimeFrames is not null && x.Horizon.TimeFrames.All(frame => frame is not null && frame.IsRequired)))
                .WithMessage("Schemas 3 through 5 require every interval in the horizon set.");
            RuleFor(x => x).Must(x => x.SchemaVersion < 5 ||
                (x.SignalMetrics is { Length: > 0 and <= 128 } && x.ObservationMetrics is { Length: > 0 and <= 32 }))
                .WithMessage("Schema 5 requires normalized signal and observation metric lists.");
            When(x=>x.SchemaVersion<3,()=>
            {
                RuleFor(x => x.StrategyParameterSetId).NotEmpty();
                RuleFor(x => x.StrategyParameterSetVersion).GreaterThan(0);
            });
            RuleFor(x => x.TargetHorizon).Must(IsTargetHorizon);
            RuleFor(x => x.Horizon).NotNull();
            When(x => x.Horizon is not null, () =>
            {
                RuleFor(x => x).Must(x => x.Horizon.TargetHorizon == x.TargetHorizon)
                    .WithMessage("Horizon target must match the parameter-set target horizon.");
                RuleFor(x => x.Horizon.TimeFrames).NotEmpty();
                When(x => x.Horizon.TimeFrames is not null, () =>
                {
                    RuleForEach(x => x.Horizon.TimeFrames).NotNull();
                    RuleForEach(x => x.Horizon.TimeFrames).Where(frame => frame is not null).ChildRules(frame =>
                    {
                        frame.RuleFor(x => x.TimeFrame).NotEqual(TimeFrameType.None);
                        frame.RuleFor(x => x.Weight).GreaterThanOrEqualTo(0m);
                        frame.RuleFor(x => x.MaximumAgeSeconds).GreaterThan(0);
                    });
                    When(x => x.Horizon.TimeFrames.All(frame => frame is not null), () =>
                    {
                        RuleFor(x => x.Horizon.TimeFrames)
                            .Must(frames => frames.Select(frame => frame.TimeFrame).Distinct().Count() == frames.Length)
                            .WithMessage("Observation timeframes must be unique.");
                        RuleFor(x => x.Horizon.TimeFrames.Sum(frame => frame.Weight)).GreaterThan(0m);
                    });
                });
            });
            RuleFor(x => x.Freshness).NotNull();
            RuleFor(x => x.DataQuality).NotNull();
            RuleFor(x => x.Trend).NotNull();
            RuleFor(x => x.Volatility).NotNull();
            RuleFor(x => x.MarketStructure).NotNull();
            RuleFor(x => x.Fusion).NotNull();
            RuleFor(x => x.Freshness.FutureClockSkewSeconds).GreaterThanOrEqualTo(0).When(x => x.Freshness is not null);
            RuleFor(x => x.DataQuality.SupportedSignalSchemaVersions).NotEmpty().When(x => x.DataQuality is not null);
            RuleFor(x => x.DataQuality.ApprovedCalculationVersions).NotEmpty().When(x => x.DataQuality is not null);
            RuleFor(x => x.DataQuality.SnapshotCaptureAttempts).InclusiveBetween(1, 10).When(x => x.DataQuality is not null);
            RuleFor(x => TrendWeight(x.Trend)).Must(IsUnitWeight).WithMessage("Trend weights must sum to one.").When(x => x.Trend is not null);
            RuleFor(x => VolatilityWeight(x.Volatility)).Must(IsUnitWeight)
                .WithMessage("Volatility weights must sum to one.").When(x => x.Volatility is not null);
            RuleFor(x => StructureWeight(x.MarketStructure)).Must(IsUnitWeight)
                .WithMessage("Market Structure weights must sum to one.").When(x => x.MarketStructure is not null);
            RuleFor(x => x.Fusion.TrendDirectionalWeight + x.Fusion.MarketStructureDirectionalWeight)
                .Must(IsUnitWeight).WithMessage("Fusion directional weights must sum to one.").When(x => x.Fusion is not null);
            RuleFor(x => x.Fusion.TrendConfidenceWeight + x.Fusion.VolatilityConfidenceWeight +
                         x.Fusion.MarketStructureConfidenceWeight)
                .Must(IsUnitWeight).WithMessage("Fusion confidence weights must sum to one.").When(x => x.Fusion is not null);
        }

        static bool IsTargetHorizon(TimeFrameType value) =>
            value is TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly;
        static bool IsUnitWeight(decimal value) => Math.Abs(value - 1m) <= 0.000001m;
        static decimal TrendWeight(TrendRegimeConfiguration value) => value.EmaAlignmentWeight +
            value.EmaSlopeWeight + value.RsiWeight + value.AdxWeight + value.MacdWeight + value.ItiWeight;
        static decimal VolatilityWeight(VolatilityRegimeConfiguration value) => value.VixWeight +
            value.AtrRatioWeight + value.TermStructureWeight + value.RealizedVolatilityWeight;
        static decimal StructureWeight(MarketStructureRegimeConfiguration value) => value.BollingerWeight +
            value.EmaInteractionWeight + value.AtrRangeWeight + value.BreakoutWeight + value.ItiWeight;
    }
}

/// <summary>Adapts parameter payload rules to the common actor validation list.</summary>
public static class RegimeDiscoveryParameterSetValidationExtensions
{
    public static List<ValidationError> ValidateRegimeDiscoveryParameterSet(
        this List<ValidationError> errors, RegimeDiscoveryParameterSet? value)
    {
        ArgumentNullException.ThrowIfNull(errors);
        errors.AddRange(new RegimeDiscoveryParameterSetValidationRules().Execute(value!));
        return errors;
    }
}
