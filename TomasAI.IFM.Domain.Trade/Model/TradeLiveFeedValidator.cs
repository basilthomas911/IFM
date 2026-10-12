using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;
public class TradeLiveFeedValidator : AbstractValidator<TradeLiveFeed>
{
    public TradeLiveFeedValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("TradeLiveFeed.OrderId is zero or negative");
        RuleFor(x => x.TradeId).GreaterThan(0).WithMessage("TradeLiveFeed.TradeId is zero or negative");
    }
}
