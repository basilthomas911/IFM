using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Actor.Option.Command.Validation;

public class OptionTradeSpreadsDataValidationRules : BaseValidationRules, IValidationRules<OptionTradeSpreadsDataModel>
{
    public const string InstanceErrorMessage = "OptionTradeSpreadsData instance is null";
    public const string OrderIdErrorMessage = "OptionTradeSpreadsData.OrderId must be > 0";
    public const string TradeIdErrorMessage = "OptionTradeSpreadsData.TradeId must be > 0";
    public const string ValueDateErrorMessage = "OptionTradeSpreadsData.ValueDate is empty";
    public const string TradeTypeErrorMessage = "OptionTradeSpreadsData.TradeType is required";
    public const string SequenceIdErrorMessage = "OptionTradeSpreadsData.SequenceId must be >= 0";
    public const string LossLimitErrorMessage = "OptionTradeSpreadsData.LossLimit must be > 0";
    public const string WinLimitErrorMessage = "OptionTradeSpreadsData.WinLimit must be > 0";
    public const string ForwardSpreadErrorMessage = "OptionTradeSpreadsData.ForwardSpread must be > 0";
    public const string NetSpreadErrorMessage = "OptionTradeSpreadsData.NetSpread must be > 0";
    public const string CreatedByErrorMessage = "OptionTradeSpreadsData.CreatedBy is required";

    public ValidationError[] Execute(OptionTradeSpreadsDataModel optionTradeSpreadsData) =>
        Validate(optionTradeSpreadsData, new OptionTradeSpreadsDataValidator());

    class OptionTradeSpreadsDataValidator : AbstractValidator<OptionTradeSpreadsDataModel>
    {
        public OptionTradeSpreadsDataValidator()
        {
            RuleFor(x => x.OrderId).GreaterThan(0).WithMessage(OrderIdErrorMessage);
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage(TradeIdErrorMessage);
            RuleFor(x => x.ValueDate).NotEmpty().WithMessage(ValueDateErrorMessage);
            RuleFor(x => x.TradeType).IsInEnum().WithMessage(TradeTypeErrorMessage);
            RuleFor(x => x.SequenceId).GreaterThanOrEqualTo(0).WithMessage(SequenceIdErrorMessage);
            RuleFor(x => x.LossLimit).GreaterThan(0m).WithMessage(LossLimitErrorMessage);
            RuleFor(x => x.WinLimit).GreaterThan(0m).WithMessage(WinLimitErrorMessage);
            RuleFor(x => x.ForwardSpread).GreaterThan(0m).WithMessage(ForwardSpreadErrorMessage);
            RuleFor(x => x.NetSpread).GreaterThan(0m).WithMessage(NetSpreadErrorMessage);
            RuleFor(x => x.CreatedBy).NotEmpty().WithMessage(CreatedByErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<OptionTradeSpreadsDataModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("OptionTradeSpreadsData", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
