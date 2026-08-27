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
    public ValidationError[] Execute(RegimeDiscoveryParameterSet value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<RegimeDiscoveryParameterSet>
    {
        public Validator()
        {
            RuleFor(x => x.ParameterSetId).NotEmpty();
            RuleFor(x => x.Version).GreaterThan(0);
            RuleFor(x => x.SchemaVersion).Equal(RegimeDiscoveryParameterSet.CurrentSchemaVersion);
            RuleFor(x => x.StrategyParameterSetId).NotEmpty();
            RuleFor(x => x.StrategyParameterSetVersion).GreaterThan(0);
            RuleFor(x => x.TargetHorizon).Must(IsTargetHorizon);
            RuleFor(x => x.Horizon).NotNull();
            RuleFor(x => x).Must(x => x.Horizon.TargetHorizon == x.TargetHorizon)
                .WithMessage("Horizon target must match the parameter-set target horizon.");
            RuleFor(x => x.Horizon.TimeFrames).NotEmpty()
                .Must(static frames => frames.Select(frame => frame.TimeFrame).Distinct().Count() == frames.Length)
                .WithMessage("Observation timeframes must be unique.");
            RuleForEach(x => x.Horizon.TimeFrames).ChildRules(frame =>
            {
                frame.RuleFor(x => x.TimeFrame).NotEqual(TimeFrameType.None);
                frame.RuleFor(x => x.Weight).GreaterThanOrEqualTo(0m);
                frame.RuleFor(x => x.MaximumAgeSeconds).GreaterThan(0);
            });
            RuleFor(x => x.Horizon.TimeFrames.Sum(frame => frame.Weight)).GreaterThan(0m);
            RuleFor(x => x.Freshness.FutureClockSkewSeconds).GreaterThanOrEqualTo(0);
            RuleFor(x => x.DataQuality.SupportedSignalSchemaVersions).NotEmpty();
            RuleFor(x => x.DataQuality.ApprovedCalculationVersions).NotEmpty();
            RuleFor(x => x.DataQuality.SnapshotCaptureAttempts).InclusiveBetween(1, 10);
            RuleFor(x => TrendWeight(x.Trend)).Must(IsUnitWeight).WithMessage("Trend weights must sum to one.");
            RuleFor(x => VolatilityWeight(x.Volatility)).Must(IsUnitWeight)
                .WithMessage("Volatility weights must sum to one.");
            RuleFor(x => StructureWeight(x.MarketStructure)).Must(IsUnitWeight)
                .WithMessage("Market Structure weights must sum to one.");
            RuleFor(x => x.Fusion.TrendDirectionalWeight + x.Fusion.MarketStructureDirectionalWeight)
                .Must(IsUnitWeight).WithMessage("Fusion directional weights must sum to one.");
            RuleFor(x => x.Fusion.TrendConfidenceWeight + x.Fusion.VolatilityConfidenceWeight +
                         x.Fusion.MarketStructureConfidenceWeight)
                .Must(IsUnitWeight).WithMessage("Fusion confidence weights must sum to one.");
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
