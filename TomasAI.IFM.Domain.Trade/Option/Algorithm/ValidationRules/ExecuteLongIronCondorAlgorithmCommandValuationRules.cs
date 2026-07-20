using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.TradeAlgorithm.Commands;

namespace TomasAI.IFM.Domain.Trade.Option.Algorithm.ValidationRules;

public class ExecuteLongIronCondorAlgorithmCommandValidationRules : BaseValidationRules, IValidationRules<ExecuteLongIronCondorAlgorithmCommand>
{
    public const string InstanceErrorMessage = "ExecuteLongIronCondorAlgorithmCommand instance is null";
    public const string OrderIdErrorMessage = "OrderId must be > 0";
    public const string TradeIdErrorMessage = "TradeId must be > 0";
    public const string ValueDateErrorMessage = "ValueDate is required";
    public const string TradeTypeErrorMessage = "TradeType is invalid";

    public ValidationError[] Execute(ExecuteLongIronCondorAlgorithmCommand command) =>
        Validate(command, new ExecuteLongIronCondorAlgorithmCommandValidator());

    class ExecuteLongIronCondorAlgorithmCommandValidator : AbstractValidator<ExecuteLongIronCondorAlgorithmCommand>
    {
        public ExecuteLongIronCondorAlgorithmCommandValidator()
        {
            RuleFor(x => x.OrderId).GreaterThan(0).WithMessage(OrderIdErrorMessage);
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage(TradeIdErrorMessage);
            RuleFor(x => x.ValueDate).NotEmpty().WithMessage(ValueDateErrorMessage);
            RuleFor(x => x.TradeType).IsInEnum().WithMessage(TradeTypeErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<ExecuteLongIronCondorAlgorithmCommand> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("ExecuteLongIronCondorAlgorithmCommand", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
