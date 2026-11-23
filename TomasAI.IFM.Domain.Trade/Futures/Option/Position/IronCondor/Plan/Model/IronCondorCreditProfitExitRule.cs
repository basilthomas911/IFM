using System.Globalization;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

public static class IronCondorCreditProfitExitRule
{
    public const decimal ProfitFraction=0.40m;
    public static TradePlanDecision? Evaluate(StrategyPositionSnapshot position,DateTime nowUtc,int maximumAgeSeconds)
    {
        if(!position.IsOpen || position.Phase!=StrategyPositionPhase.MarkToMarket || nowUtc.Kind!=DateTimeKind.Utc
            || maximumAgeSeconds<=0 || position.AsOfUtc>nowUtc || nowUtc-position.AsOfUtc>TimeSpan.FromSeconds(maximumAgeSeconds)
            || position.Legs.Length!=4 || position.Legs.Any(l=>l.SignedQuantity is 0 or int.MinValue || l.OpeningPrice<=0)
            || position.Legs.Select(l=>Math.Abs(l.SignedQuantity)).Distinct().Count()!=1)return null;
        var entry=-position.Legs.Sum(l=>l.OpeningPrice*l.SignedQuantity);
        if(entry<=0)return null;
        decimal debit=0;
        foreach(var leg in position.Legs)
        {
            var quote=StrategyPositionLegOptionData.Observe(leg);
            if(quote.QuoteAtUtc is not {Kind:DateTimeKind.Utc} at || at>nowUtc || nowUtc-at>TimeSpan.FromSeconds(maximumAgeSeconds)
                || quote.BidPrice is not {} bid || quote.AskPrice is not {} ask || bid<=0 || ask<=0 || bid>ask)return null;
            debit-=(leg.SignedQuantity<0?ask:bid)*leg.SignedQuantity;
        }
        if(entry-debit<ProfitFraction*entry)return null;
        var quantity=Math.Abs(position.Legs[0].SignedQuantity);
        return new(TradePlanState.ExitRequired,TradePlanAction.ExitAtMarket,true,"IronCondorTradePlan.CREDIT_PROFIT_TARGET_REACHED",
            string.Create(CultureInfo.InvariantCulture,$"Executable closing debit {debit/quantity:G8} points is <= 60% of opening credit {entry/quantity:G8}; at least 40% gross credit profit reached; close all remaining condor legs."));
    }
}
