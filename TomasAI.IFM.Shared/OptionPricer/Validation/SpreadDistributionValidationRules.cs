using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.Shared.OptionPricer.Validation;

public class SpreadDistributionValidationRules : BaseValidationRules, IValidationRules<SpreadDistributionReadModel>
{
    public const string InstanceErrorMessage = "SpreadDistribution instance is null";
    public const string ValueDateErrorMessage = "SpreadDistribution.ValueDate is required";
    public const string TradeIdErrorMessage = "SpreadDistribution.TradeId is required";
    public const string DaysToExpiryErrorMessage = "SpreadDistribution.DaysToExpiry is negative";
    public const string ForwardPriceErrorMessage = "SpreadDistribution.ForwardPrice is invalid";
    public const string LossProbabilityErrorMessage = "SpreadDistribution.LossProbability is invalid";
    public const string ShortVolatilityErrorMessage = "SpreadDistribution.ShortVolatility is invalid";
    public const string LongVolatilityErrorMessage = "SpreadDistribution.LongVolatility is invalid";
    public const string ForwardLossRatioErrorMessage = "SpreadDistribution.ForwardLossRatio is invalid";

    public ValidationError[] Execute(SpreadDistributionReadModel spreadDistribution) => Validate(spreadDistribution, new SpreadDistributionValidator());

    class SpreadDistributionValidator : AbstractValidator<SpreadDistributionReadModel>
    {
        public SpreadDistributionValidator()
        {
            RuleFor(x => x.TradeId).NotEmpty().WithMessage(TradeIdErrorMessage);
            RuleFor(x => x.ValueDate).NotEmpty().WithMessage(ValueDateErrorMessage);
            RuleFor(x => x.DaysToExpiry).GreaterThanOrEqualTo(0).WithMessage(DaysToExpiryErrorMessage);
            RuleFor(x => x.ForwardPrice).Must(e => !double.IsNaN(e)).WithMessage(ForwardPriceErrorMessage);
            RuleFor(x => x.LossProbability).Must(e => !double.IsNaN(e)).WithMessage(LossProbabilityErrorMessage);
            RuleFor(x => x.ShortVolatility).Must(e => !double.IsNaN(e)).WithMessage(ShortVolatilityErrorMessage);
            RuleFor(x => x.LongVolatility).Must(e => !double.IsNaN(e)).WithMessage(LongVolatilityErrorMessage);
            RuleFor(x => x.ForwardLossRatio).Must(e => !double.IsNaN(e)).WithMessage(ForwardLossRatioErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<SpreadDistributionReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch 
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("SpreadDistribution", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
