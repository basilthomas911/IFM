using FluentValidation;
using FluentValidation.Results;
using MessagePack;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared;

/// <summary>Validates a Traders Dynamic Index configuration before calculation or routing.</summary>
public sealed class FuturesTdiConfigurationValidationRules
    : BaseValidationRules, IValidationRules<FuturesTdiConfiguration>
{
    static readonly Validator Rules = new();

    public ValidationError[] Execute(FuturesTdiConfiguration configuration) => Validate(configuration, Rules);

    sealed class Validator : AbstractValidator<FuturesTdiConfiguration>
    {
        public Validator()
        {
            RuleFor(x => x.ConfigurationId).NotEmpty();
            RuleFor(x => x.RsiPeriod).GreaterThan(1);
            RuleFor(x => x.PriceLinePeriod).GreaterThan(0);
            RuleFor(x => x.SignalLinePeriod).GreaterThan(0);
            RuleFor(x => x.MarketBasePeriod).GreaterThan(1);
            RuleFor(x => x.VolatilityBandPeriod).GreaterThan(1);
            RuleFor(x => x.VolatilityBandDeviation).GreaterThan(0d);
            RuleFor(x => x.OversoldLevel).InclusiveBetween(0d, 100d);
            RuleFor(x => x.Midline).InclusiveBetween(0d, 100d);
            RuleFor(x => x.OverboughtLevel).InclusiveBetween(0d, 100d);
            RuleFor(x => x).Must(x => x.OversoldLevel < x.Midline && x.Midline < x.OverboughtLevel)
                .WithMessage("TDI levels must satisfy OversoldLevel < Midline < OverboughtLevel");
            RuleFor(x => x.Version).GreaterThan(0);
        }

        public override ValidationResult Validate(ValidationContext<FuturesTdiConfiguration> context)
        {
            if (context.InstanceToValidate is null)
                return new ValidationResult([new ValidationFailure(nameof(FuturesTdiConfiguration), "TDI configuration is null")]);
            return base.Validate(context);
        }
    }
}
