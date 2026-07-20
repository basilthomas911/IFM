using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Trade.Validation;

public class OptionLegDataValidationRules : BaseValidationRules, IValidationRules<OptionTradeLegDataReadModel>
{
    public const string InstanceErrorMessage = "OptionLegsData instance is null";
    public const string TradeIdErrorMessage = "OptionLegsData.TradeId must be > 0";
    public const string OptionLegIdErrorMessage = "OptionLegsData.OptionLegId is required";
    public const string ValueDateErrorMessage = "OptionLegsData.ValueDate is empty";
    public const string TradeTypeErrorMessage = "OptionLegsData.TradeType is required";
    public const string DaysToExpiryErrorMessage = "OptionLegsData.DaysToExpiry must be >= 0";
    public const string BidPriceErrorMessage = "OptionLegsData.BidPrice must be >= 0";
    public const string AskPriceErrorMessage = "OptionLegsData.AskPrice must be >= 0";
    public const string ImpliedVolatilityErrorMessage = "OptionLegsData.ImpliedVolatility must be >= 0";
    public const string DeltaErrorMessage = "OptionLegsData.Delta must be between -1 and 1";
    public const string GammaErrorMessage = "OptionLegsData.Gamma must be >= 0";
    public const string ThetaErrorMessage = "OptionLegsData.Theta must be <= 0";
    public const string VegaErrorMessage = "OptionLegsData.Vega must be >= 0";
    public const string RhoErrorMessage = "OptionLegsData.Rho must be between -1 and 1";
    public const string CreatedByErrorMessage = "OptionLegsData.CreatedBy is required";
    public const string UpdatedByErrorMessage = "OptionLegsData.UpdatedBy is required";

    public ValidationError[] Execute(OptionTradeLegDataReadModel optionLegsData) => Validate(optionLegsData, new OptionLegDataValidator());

    class OptionLegDataValidator : AbstractValidator<OptionTradeLegDataReadModel>
    {
        public OptionLegDataValidator()
        {
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage(TradeIdErrorMessage);
            RuleFor(x => x.OptionLegId).NotEmpty().WithMessage(OptionLegIdErrorMessage);
            RuleFor(x => x.ValueDate).NotEmpty().WithMessage(ValueDateErrorMessage);
            RuleFor(x => x.TradeType).IsInEnum().WithMessage(TradeTypeErrorMessage);
            RuleFor(x => x.DaysToExpiry).GreaterThanOrEqualTo(0).WithMessage(DaysToExpiryErrorMessage);
            RuleFor(x => x.BidPrice).GreaterThanOrEqualTo(0m).WithMessage(BidPriceErrorMessage);
            RuleFor(x => x.AskPrice).GreaterThanOrEqualTo(0m).WithMessage(AskPriceErrorMessage);
            RuleFor(x => x.ImpliedVolatility).GreaterThanOrEqualTo(0d).WithMessage(ImpliedVolatilityErrorMessage);
            RuleFor(x => x.Delta).InclusiveBetween(-1d, 1d).WithMessage(DeltaErrorMessage);
            RuleFor(x => x.Gamma).GreaterThanOrEqualTo(0d).WithMessage(GammaErrorMessage);
            RuleFor(x => x.Theta).LessThanOrEqualTo(0d).WithMessage(ThetaErrorMessage);
            RuleFor(x => x.Vega).GreaterThanOrEqualTo(0d).WithMessage(VegaErrorMessage);
            RuleFor(x => x.Rho).InclusiveBetween(-1d, 1d).WithMessage(RhoErrorMessage);
            RuleFor(x => x.CreatedBy).NotEmpty().WithMessage(CreatedByErrorMessage);
            RuleFor(x => x.UpdatedBy).NotEmpty().WithMessage(UpdatedByErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<OptionTradeLegDataReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("OptionLegsData", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
