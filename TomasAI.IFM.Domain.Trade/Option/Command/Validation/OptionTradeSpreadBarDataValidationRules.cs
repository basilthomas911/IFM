using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command.Validation;

public class OptionTradeSpreadBarDataValidationRules : BaseValidationRules, IValidationRules<OptionTradeSpreadBarsDataModel>
{
    public const string InstanceErrorMessage = "OptionTradeSpreadBarData instance is null";
    public const string OrderIdErrorMessage = "OptionTradeSpreadBarData.OrderId must be > 0";
    public const string TradeIdErrorMessage = "OptionTradeSpreadBarData.TradeId must be > 0";
    public const string ValueDateErrorMessage = "OptionTradeSpreadBarData.ValueDate is empty";
    public const string BarDateErrorMessage = "OptionTradeSpreadBarData.BarDate is empty";
    public const string LossLimitErrorMessage = "OptionTradeSpreadBarData.LossLimit must be > 0";
    public const string WinLimitErrorMessage = "OptionTradeSpreadBarData.WinLimit must be > 0";
    public const string ForwardSpreadErrorMessage = "OptionTradeSpreadBarData.ForwardSpread must be > 0";
    public const string NetSpreadErrorMessage = "OptionTradeSpreadBarData.NetSpread must be > 0";

    public ValidationError[] Execute(OptionTradeSpreadBarsDataModel optionTradeSpreadBarData) => Validate(optionTradeSpreadBarData, new OptionTradeSpreadBarDataValidator());

    class OptionTradeSpreadBarDataValidator : AbstractValidator<OptionTradeSpreadBarsDataModel>
    {
        public OptionTradeSpreadBarDataValidator()
        {
            RuleFor(x => x.OrderId).GreaterThan(0).WithMessage(OrderIdErrorMessage);
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage(TradeIdErrorMessage);
            RuleFor(x => x.ValueDate).NotEmpty().WithMessage(ValueDateErrorMessage);
            RuleFor(x => x.BarDate).NotEmpty().WithMessage(BarDateErrorMessage);
            RuleFor(x => x.LossLimit).GreaterThan(0.0m).WithMessage(LossLimitErrorMessage);
            RuleFor(x => x.WinLimit).GreaterThan(0.0m).WithMessage(WinLimitErrorMessage);
            RuleFor(x => x.ForwardSpread).GreaterThan(0m).WithMessage(ForwardSpreadErrorMessage);
            RuleFor(x => x.NetSpread).GreaterThan(0m).WithMessage(NetSpreadErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<OptionTradeSpreadBarsDataModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch 
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("OptionTradeSpreadBarData", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
