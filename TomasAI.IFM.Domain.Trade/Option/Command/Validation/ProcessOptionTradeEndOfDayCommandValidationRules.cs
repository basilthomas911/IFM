using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade.Commands;

namespace TomasAI.IFM.Domain.Trade.Option.Command.Validation;

public class ProcessOptionTradeEndOfDayCommandValidationRules 
    : BaseValidationRules, IValidationRules<ProcessOptionTradeEndOfDayCommand>
{
    public const string InstanceErrorMessage = "ProcessOptionTradeEndOfDayCommand instance is null";
    public const string FundIdErrorMessage = "FundId must be > 0";
    public const string OrderIdErrorMessage = "OrderId must be > 0";
    public const string TradeIdErrorMessage = "TradeId must be > 0";
    public const string ValueDateErrorMessage = "ValueDate is required";
    public const string TradeTypeErrorMessage = "TradeType is required";
    public const string TradeStatusErrorMessage = "TradeStatus is required";
    public const string OpenPriceErrorMessage = "OpenPrice must be >= 0";
    public const string HighPriceErrorMessage = "HighPrice must be >= 0";
    public const string LowPriceErrorMessage = "LowPrice must be >= 0";
    public const string ClosePriceErrorMessage = "ClosePrice must be >= 0";
    public const string VolumeErrorMessage = "Volume must be >= 0";
    public const string ReferenceErrorMessage = "Reference is required";

    public ValidationError[] Execute(ProcessOptionTradeEndOfDayCommand command) =>
        Validate(command, new ProcessOptionTradeEndOfDayCommandValidator());

    class ProcessOptionTradeEndOfDayCommandValidator : AbstractValidator<ProcessOptionTradeEndOfDayCommand>
    {
        public ProcessOptionTradeEndOfDayCommandValidator()
        {
            RuleFor(x => x.FundId).GreaterThan(0).WithMessage(FundIdErrorMessage);
            RuleFor(x => x.OrderId).GreaterThan(0).WithMessage(OrderIdErrorMessage);
            RuleFor(x => x.TradeId).GreaterThan(0).WithMessage(TradeIdErrorMessage);
            RuleFor(x => x.ValueDate).NotEmpty().WithMessage(ValueDateErrorMessage);
            RuleFor(x => x.TradeType).IsInEnum().WithMessage(TradeTypeErrorMessage);
            RuleFor(x => x.TradeStatus).IsInEnum().WithMessage(TradeStatusErrorMessage);
            RuleFor(x => x.OpenPrice).GreaterThanOrEqualTo(0).WithMessage(OpenPriceErrorMessage);
            RuleFor(x => x.HighPrice).GreaterThanOrEqualTo(0).WithMessage(HighPriceErrorMessage);
            RuleFor(x => x.LowPrice).GreaterThanOrEqualTo(0).WithMessage(LowPriceErrorMessage);
            RuleFor(x => x.ClosePrice).GreaterThanOrEqualTo(0).WithMessage(ClosePriceErrorMessage);
            RuleFor(x => x.Volume).GreaterThanOrEqualTo(0).WithMessage(VolumeErrorMessage);
            RuleFor(x => x.Reference).NotEmpty().WithMessage(ReferenceErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<ProcessOptionTradeEndOfDayCommand> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("ProcessOptionTradeEndOfDayCommand", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
