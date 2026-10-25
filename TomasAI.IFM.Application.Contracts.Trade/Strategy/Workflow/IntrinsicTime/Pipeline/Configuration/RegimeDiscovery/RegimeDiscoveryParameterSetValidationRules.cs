using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the related validation rules together so the full contract is checked in the established order.")]
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
            When(x => x.SchemaVersion < 3, () =>
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
