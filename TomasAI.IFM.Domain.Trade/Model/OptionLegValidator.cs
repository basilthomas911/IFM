using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Model;
public class OptionLegValidator : AbstractValidator<OptionLeg>
{
    public OptionLegValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("OptionLeg.OrderId is zero or negative");
        RuleFor(x => x.TradeId).GreaterThan(0).WithMessage("OptionLeg.TradeId is zero or negative");
        RuleFor(x => x.ContractId).NotEmpty().WithMessage("OptionLeg.ContractId is empty");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("OptionLeg.Quantity is zero or negative");
        RuleFor(x => x.StrikePrice).GreaterThan(0m).WithMessage("SpreadTrade.StrikePrice is zero or negative");
    }
}
