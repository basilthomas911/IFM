using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Validation;

public class FuturesTradeSignalValidationRules : BaseValidationRules, IValidationRules<FuturesTradeSignalV2ReadModel>
{
    static readonly FuturesTradeSignalValidator Validator = new();

    public ValidationError[] Execute(FuturesTradeSignalV2ReadModel futuresTradeSignal) => Validate(futuresTradeSignal, Validator);

    private class FuturesTradeSignalValidator : AbstractValidator<FuturesTradeSignalV2ReadModel>
    {
        public FuturesTradeSignalValidator()
        {
            RuleFor(x => x.ContractId).NotEmpty().WithMessage("FuturesTradeSignqal.ContractId is required");
            RuleFor(x => x.ValueDate).NotEmpty().WithMessage("FuturesTradeSignqal.ValueDate is required");
            RuleFor(x => x.StdDev).Must(stdDev => !double.IsNaN(stdDev)).WithMessage("FuturesTradeSignqal.StdDev is NaN");
            RuleFor(x => x.FuturesPrice).Must(futuresPrice => !double.IsNaN(futuresPrice)).WithMessage("FuturesTradeSignqal.FuturesPrice is NaN");
            RuleFor(x => x.FundRiskPercent).Must(fundRiskPercent => !double.IsNaN(fundRiskPercent)).WithMessage("FuturesTradeSignqal.FundRiskPercent is NaN");
            RuleFor(x => x.RSI).Must(upTrendLimit => !double.IsNaN(upTrendLimit)).WithMessage("FuturesTradeSignqal.RSI is NaN");
            RuleFor(x => x.RSISlope).Must(downTrendLimit => !double.IsNaN(downTrendLimit)).WithMessage("FuturesTradeSignqal.RSISlope is NaN");
        }

        public override ValidationResult Validate(ValidationContext<FuturesTradeSignalV2ReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch 
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("FuturesTradeSignqal", "FuturesTradeSignqal instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
