using FluentValidation;
using FluentValidation.Results;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Shared.TradeOrder.Validation;

public class TradeOrderValidationRules : BaseValidationRules, IValidationRules<TradeOrderReadModel>
{
    public ValidationError[] Execute(TradeOrderReadModel tradeOrder) => Validate(tradeOrder, new TradeOrderValidator());

    sealed class TradeOrderValidator : AbstractValidator<TradeOrderReadModel>
    {
        public TradeOrderValidator()
        {
            RuleFor(x => x.FundId).NotEmpty().GreaterThan(0).WithMessage("TradeOrder.FundId is required");
            RuleFor(x => x.OrderId).NotEmpty().GreaterThan(0).WithMessage("TradeOrder.OrderId is required");
            RuleFor(x => x.TradeId).NotEmpty().GreaterThan(0).WithMessage("TradeOrder.TradeId is required");
            RuleFor(x => x.ValueDate).NotEmpty().WithMessage("TradeOrder.ValueDate is invalid");
            RuleFor(x => x.TradeDate).NotEmpty().WithMessage("TradeOrder.TradeDate is invalid");
            RuleFor(x => x.MaturityDate).NotEmpty().WithMessage("TradeOrder.MaturityDate is invalid");
            RuleFor(x => x.OrderQuantity).NotEmpty().GreaterThan(0).WithMessage("TradeOrder.OrderQuantity is required");
            RuleFor(x => x.OrderPrice).NotEmpty().WithMessage("TradeOrder.OrderPrice is empty");
            RuleFor(x => x.OrderAmount).NotEmpty().WithMessage("TradeOrder.OrderAmount is empty");
            RuleFor(x => x.Commission).NotEmpty().WithMessage("TradeOrder.Commission is empty");
            RuleFor(x => x.TotalAmount).NotEmpty().WithMessage("TradeOrder.TotalAmount is empty");
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Convert malformed model exceptions into the existing aggregated validation failures.")]
        public override ValidationResult Validate(ValidationContext<TradeOrderReadModel> context)
        {
            try
            {
                if (context.InstanceToValidate is null)
                    throw new ArgumentNullException(nameof(context));
            }
            catch
            {
                var validationResult = new ValidationResult();
                validationResult.Errors.Add(new ValidationFailure("TradeOrder", "TradeOrder instance is null"));
                return validationResult;
            }
            return base.Validate(context);
        }
    }
}
