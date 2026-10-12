using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;
public class TradeFillValidator : AbstractValidator<TradeFill>
{
    public TradeFillValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("TradeFill.OrderId is zero or negative");
        RuleFor(x => x.TradeId).GreaterThan(0).WithMessage("TradeFill.TradeId is zero or negative");
        RuleFor(x => x.FillDate).Must(e => e != DateTime.MinValue && e != DateTime.MaxValue).WithMessage("TradeFill.FillDate is invalid date");
        RuleFor(x => x.FillQuantity).GreaterThan(0).WithMessage("TradeFill.FillQuantity is zero or negative");
    }
}
