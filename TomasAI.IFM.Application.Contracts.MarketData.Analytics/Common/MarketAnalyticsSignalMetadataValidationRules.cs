using FluentValidation;
using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

/// <summary>Validates common analytics signal metadata.</summary>
public sealed class MarketAnalyticsSignalMetadataValidationRules
    : BaseValidationRules, IValidationRules<MarketAnalyticsSignalMetadata>
{
    static readonly Validator Rules = new();

    /// <summary>Validates the supplied signal metadata.</summary>
    /// <param name="value">Metadata to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(MarketAnalyticsSignalMetadata value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<MarketAnalyticsSignalMetadata>
    {
        public Validator()
        {
            RuleFor(x => x.SignalKey)
                .Must(x => new MarketAnalyticsSignalKeyValidationRules().Execute(x).Length == 0);
            RuleFor(x => x.ContractId).NotEmpty();
            RuleFor(x => x.ValueDate).Must(x => x != DateOnly.MinValue && x != DateOnly.MaxValue);
            RuleFor(x => x.ObservationId.Value).NotEmpty();
            RuleFor(x => x.MarketDataAsOfUtc).Must(IsUtc);
            RuleFor(x => x.CalculatedAtUtc).Must(IsUtc);
            RuleFor(x => x).Must(x => x.CalculatedAtUtc >= x.MarketDataAsOfUtc)
                .WithMessage("CalculatedAtUtc must not precede MarketDataAsOfUtc.");
            RuleFor(x => x.SourceSequence).GreaterThanOrEqualTo(0);
            RuleFor(x => x.SchemaVersion).GreaterThan((ushort)0);
            RuleFor(x => x.CalculationVersion).NotEmpty();
            RuleFor(x => x.CalculationMethod).IsInEnum().NotEqual(MarketSignalCalculationMethod.Unknown);
            RuleFor(x => x.ValidationIssues).NotNull();
            RuleFor(x => x).Must(x => !x.IsValid || x.ValidationIssues.Length == 0)
                .WithMessage("A valid signal cannot contain validation issues.");
        }

        static bool IsUtc(DateTimeOffset value) => value.Offset == TimeSpan.Zero;
    }
}
