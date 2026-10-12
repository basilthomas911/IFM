using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using FluentValidation;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using MathNet.Numerics;

namespace TomasAI.IFM.Domain.Trade.Model;
public class OptionLegDataValidator : AbstractValidator<OptionLegData>
{
    public OptionLegDataValidator()
    {
        RuleFor(x => x.OrderId).GreaterThan(0).WithMessage("OptionLegData.OrderId is zero or negative");
        RuleFor(x => x.TradeId).GreaterThan(0).WithMessage("OptionLegData.TradeId is zero or negative");
    }
}
