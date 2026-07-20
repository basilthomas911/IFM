using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.TradeAlgorithm.Commands;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm.ValidationRules;

public class ExecuteShortIronCondorAlgorithmCommandValidationRules : BaseValidationRules, IValidationRules<ExecuteShortIronCondorAlgorithmCommand>
{
    public const string InstanceErrorMessage = "ExecuteShortIronCondorAlgorithmCommand instance is null";
    public const string OrderIdErrorMessage = "OrderId must be > 0";
    public const string TradeIdErrorMessage = "TradeId must be > 0";
    public const string ValueDateErrorMessage = "ValueDate is required";
    public const string OptionTradesErrorMessage = "OptionTrades is required";
    public const string FuturesContractErrorMessage = "FuturesContract is required";
    public const string FuturesTradeSignalErrorMessage = "FuturesTradeSignal is required";

    public ValidationError[] Execute(ExecuteShortIronCondorAlgorithmCommand command) =>
        Validate(command, new ExecuteShortIronCondorAlgorithmCommandValidator());

    class ExecuteShortIronCondorAlgorithmCommandValidator : AbstractValidator<ExecuteShortIronCondorAlgorithmCommand>
    {
        public ExecuteShortIronCondorAlgorithmCommandValidator()
        {
            RuleFor(x => x.OrderId).GreaterThan(0).WithMessage(OrderIdErrorMessage);
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage(TradeIdErrorMessage);
            RuleFor(x => x.ValueDate).NotEmpty().WithMessage(ValueDateErrorMessage);
            RuleFor(x => x.OptionTrades).NotNull().WithMessage(OptionTradesErrorMessage);
            RuleFor(x => x.FuturesContract).NotNull().WithMessage(FuturesContractErrorMessage);
            RuleFor(x => x.FuturesTradeSignal).NotNull().WithMessage(FuturesTradeSignalErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<ExecuteShortIronCondorAlgorithmCommand> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("ExecuteShortIronCondorAlgorithmCommand", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
