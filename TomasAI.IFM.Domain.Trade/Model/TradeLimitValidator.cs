using TomasAI.IFM.Domain.Trade.Shared;
using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;
public class TradeLimitValidator : AbstractValidator<TradeLimit>
{
    public TradeLimitValidator()
    {
        RuleFor(x => x.TradeId).GreaterThan(0).WithMessage("TradeLimit.TradeId is zero or negative");
    }
}
