using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.ForwardPrice.Function;

/// <summary>Captures one immutable set of current position/pricing observations without provider calls.</summary>
public static class CaptureIronCondorForwardPrice
{
    /// <summary>Projects retained leg values and exact contract conventions into Trade.Shared inputs.</summary>
    public static IronCondorForwardPriceInputs CaptureForwardPriceInputs(this StrategyPositionSnapshot position, IronCondorTradePlanInputs? inputs)
    {
        var legs = position.Legs.Select(leg =>
        {
            var retained = StrategyPositionLegOptionData.Observe(leg);
            var fallback = inputs?.DailyRiskInputs?.Legs.SingleOrDefault(l => l.TradeLegId == leg.TradeLegId && l.ContractId == leg.ContractId);
            var calculated = inputs?.CalculatedSpreadPrices?.OptionLegPrices.SingleOrDefault(l => l.ContractId == leg.ContractId);
            var pricing = leg.OptionRisk?.PricingInputs ?? leg.OptionTick?.PricingInputs;
            return new IronCondorForwardPriceLeg(leg.TradeLegId, leg.ContractId, leg.PutCall == 1, leg.SignedQuantity,
                (double?)(retained.TheoreticalPrice ?? fallback?.TheoreticalPrice) ?? calculated?.TheoreticalPrice ?? double.NaN,
                retained.Delta ?? fallback?.Delta ?? double.NaN, leg.CurrentPrice,
                pricing?.Multiplier ?? inputs?.DailyRiskInputs?.ContractMultiplier ?? 0,
                pricing?.Currency ?? inputs?.DailyRiskInputs?.Currency ?? "");
        }).ToArray();
        return new() { TradeDate = inputs?.TradeDate ?? default, MaturityDate = inputs?.MaturityDate ?? default, OptionLegs = legs };
    }
}
