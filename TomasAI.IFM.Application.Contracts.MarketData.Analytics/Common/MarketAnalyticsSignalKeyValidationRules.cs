using FluentValidation;
using MessagePack;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

/// <summary>Validates a configured analytics signal key.</summary>
public sealed class MarketAnalyticsSignalKeyValidationRules
    : BaseValidationRules, IValidationStructRules<MarketAnalyticsSignalKey>
{
    static readonly Validator Rules = new();

    /// <summary>Validates the supplied signal key.</summary>
    /// <param name="value">Signal key to validate.</param>
    /// <returns>All validation errors, or an empty array when valid.</returns>
    public ValidationError[] Execute(MarketAnalyticsSignalKey value) => Validate(value, Rules);

    sealed class Validator : AbstractValidator<MarketAnalyticsSignalKey>
    {
        public Validator()
        {
            RuleFor(x => x.MarketSeriesIdentity)
                .Must(x => new MarketSeriesIdentityValidationRules().Execute(x).Length == 0);
            RuleFor(x => x.SignalKind).IsInEnum().NotEqual(MarketAnalyticsSignalKind.Unknown);
            RuleFor(x => x.TimeFrame).IsInEnum().NotEqual(TimeFrameType.None);
            RuleFor(x => x.CalculationConfigurationId).NotEmpty();
        }
    }
}
