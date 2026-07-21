using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade.Commands;

namespace TomasAI.IFM.Domain.Trade.Option.Command.Validation;

public class ChangeOptionTradeLegDataCommandValidationRules : BaseValidationRules, IValidationRules<ChangeOptionTradeLegDataCommand>
{
    public const string InstanceErrorMessage = "ChangeOptionTradeLegDataCommand instance is null";
    public const string OrderIdErrorMessage = "OrderId must be > 0";
    public const string TradeIdErrorMessage = "TradeId must be > 0";
    public const string AssetPriceErrorMessage = "AssetPrice must be > 0";
    public const string RiskFreeRateErrorMessage = "RiskFreeRate must be a valid number";
    public const string ValueDateErrorMessage = "ValueDate is required";
    public const string OptionLegDataErrorMessage = "OptionLegData is required";

    public ValidationError[] Execute(ChangeOptionTradeLegDataCommand command) =>
        Validate(command, new ChangeOptionTradeLegDataCommandValidator());

    class ChangeOptionTradeLegDataCommandValidator : AbstractValidator<ChangeOptionTradeLegDataCommand>
    {
        public ChangeOptionTradeLegDataCommandValidator()
        {
            RuleFor(x => x.OrderId).GreaterThan(0).WithMessage(OrderIdErrorMessage);
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage(TradeIdErrorMessage);
            RuleFor(x => x.ValueDate).NotEmpty().WithMessage(ValueDateErrorMessage);
            RuleFor(x => x.AssetPrice).GreaterThanOrEqualTo(0m).WithMessage(AssetPriceErrorMessage);
            RuleFor(x => x.RiskFreeRate).Must(x => !double.IsNaN(x)).WithMessage(RiskFreeRateErrorMessage);
            RuleFor(x => x.OptionLegData).NotNull().WithMessage(OptionLegDataErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<ChangeOptionTradeLegDataCommand> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("ChangeOptionTradeLegDataCommand", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}

