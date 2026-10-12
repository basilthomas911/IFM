using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;

/// <summary>Validates an immutable futures analytics observation.</summary>
public sealed class FuturesTradeSessionBarReadModelValidationRules
    : BaseValidationRules, IValidationRules<FuturesTradeSessionBarReadModel>
{
    static readonly Validator Rules = new();

    /// <summary>Validates the supplied observation.</summary>
    /// <param name="value">Observation to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(FuturesTradeSessionBarReadModel value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<FuturesTradeSessionBarReadModel>
    {
        public Validator()
        {
            RuleFor(x => x.MarketSeriesIdentity)
                .Must(x => new MarketSeriesIdentityValidationRules().Execute(x).Length == 0);
            RuleFor(x => x.ObservationId.Value).NotEmpty();
            RuleFor(x => x).Must(HasMatchingObservationId)
                .WithMessage("ObservationId must match the immutable series, timeframe, interval end, and source sequence.");
            RuleFor(x => x.ContractId).NotEmpty();
            RuleFor(x => x.ValueDate).Must(x => x != DateOnly.MinValue && x != DateOnly.MaxValue);
            RuleFor(x => x.TimeFrame).IsInEnum().NotEqual(TimeFrameType.None);
            RuleFor(x => x.IntervalStartUtc).Must(IsUtc);
            RuleFor(x => x.IntervalEndUtc).Must(IsUtc);
            RuleFor(x => x).Must(x => x.IntervalEndUtc > x.IntervalStartUtc)
                .WithMessage("IntervalEndUtc must follow IntervalStartUtc.");
            RuleFor(x => x).Must(HasConsistentOhlc)
                .WithMessage("OHLC values must have a consistent high and low range.");
            RuleFor(x => x.Volume).GreaterThanOrEqualTo(0);
            RuleFor(x => x.TradeCount).GreaterThanOrEqualTo(0);
            RuleFor(x => x.PriceVolumeSum).GreaterThanOrEqualTo(0);
            RuleFor(x => x.FirstSourceSequence).GreaterThanOrEqualTo(0);
            RuleFor(x => x).Must(x => x.LastSourceSequence >= x.FirstSourceSequence)
                .WithMessage("LastSourceSequence must not precede FirstSourceSequence.");
            RuleFor(x => x.FirstMarketEventUtc).Must(IsUtc);
            RuleFor(x => x.LastMarketEventUtc).Must(IsUtc);
            RuleFor(x => x).Must(x => x.LastMarketEventUtc >= x.FirstMarketEventUtc)
                .WithMessage("LastMarketEventUtc must not precede FirstMarketEventUtc.");
            // Exchange event time and the host finalization clock are independently sourced.
            // Preserve both real timestamps; their order cannot establish bar validity.
            RuleFor(x => x.CalculatedAtUtc).Must(x => x != default && IsUtc(x));
            RuleFor(x => x.SchemaVersion).GreaterThan((ushort)0);
            RuleFor(x => x.CalculationVersion).NotEmpty();
            RuleFor(x => x.CalculationMethod).IsInEnum().NotEqual(MarketSignalCalculationMethod.Unknown);
            RuleFor(x => x.StreamEpochId).NotEmpty()
                .When(x => x.SchemaVersion >= 2
                    && x.CalculationMethod == MarketSignalCalculationMethod.ClosedObservation);
            RuleFor(x => x.ValidationIssues).NotNull();
            RuleFor(x => x).Must(x => !x.IsValid || (x.IsComplete && x.ValidationIssues is { Length: 0 }))
                .WithMessage("A valid observation must be complete and contain no validation issues.");
        }

        static bool IsUtc(DateTimeOffset value) => value.Offset == TimeSpan.Zero;

        static bool HasConsistentOhlc(FuturesTradeSessionBarReadModel value) =>
            value.High >= value.Low
            && value.High >= value.Open
            && value.High >= value.Close
            && value.Low <= value.Open
            && value.Low <= value.Close;

        static bool HasMatchingObservationId(FuturesTradeSessionBarReadModel value)
        {
            try
            {
                return value.ObservationId == FuturesTradeSessionBarId.Create(
                    value.MarketSeriesIdentity,
                    value.TimeFrame,
                    value.IntervalEndUtc,
                    value.LastSourceSequence);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
