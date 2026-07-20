using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Plan.ForwardLossLimit.Validation;

public class TradePlanForwardLossLimitValidationRules
    : BaseValidationRules, IValidationRules<TradePlanForwardLossLimitReadModel>
{
    public const string InstanceErrorMessage = "TradePlan instance is null";
    public const string OrderIdErrorMessage = "TradePlan.OrderId is < 1";
    public const string TradeIdErrorMessage = "TradePlan.TradeId is < 1";
    public const string ValueDateErrorMessage = "TradePlan.ValueDate is empty";
    public const string ActionDateErrorMessage = "TradePlan.ActionDate is empty";
    public const string LimitTypeErrorMessage = "TradePlan.LimitType must not be Unknown";

    public ValidationError[] Execute(TradePlanForwardLossLimitReadModel forwardLossLimit) => Validate(forwardLossLimit, new TradePlanForwardLossLimitValidator());

    class TradePlanForwardLossLimitValidator : AbstractValidator<TradePlanForwardLossLimitReadModel>
    {
        public TradePlanForwardLossLimitValidator()
        {
            RuleFor(x => x.OrderId).Must(e => e > 0).WithMessage(OrderIdErrorMessage);
            RuleFor(x => x.TradeId).Must(e => e > 0).WithMessage(TradeIdErrorMessage);
            RuleFor(x => x.ValueDate).NotEmpty().WithMessage(ValueDateErrorMessage);
            RuleFor(x => x.LimitType).Must(e => e != Shared.Trade.ForwardLossLimitType.Unknown ).WithMessage(LimitTypeErrorMessage);
        }

        public override ValidationResult Validate(ValidationContext<TradePlanForwardLossLimitReadModel> context)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(context.InstanceToValidate);
            }
            catch 
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("TradePlanForwardLossLimit", InstanceErrorMessage));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
