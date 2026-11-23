using System.Globalization;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;

/// <summary>Independent current-Greek protection; only fresh, remaining short legs can trigger.</summary>
public static class IronCondorShortDeltaExitRule
{
    public const double Limit = 0.30;
    public static TradePlanDecision? Evaluate(StrategyPositionSnapshot position, DateTime nowUtc, int maximumAgeSeconds)
    {
        if(!position.IsOpen || position.Phase!=StrategyPositionPhase.MarkToMarket || nowUtc.Kind!=DateTimeKind.Utc
            || maximumAgeSeconds<=0 || position.AsOfUtc>nowUtc || nowUtc-position.AsOfUtc>TimeSpan.FromSeconds(maximumAgeSeconds))return null;
        var hits=new List<string>();var call=false;var put=false;
        foreach(var leg in position.Legs.Where(l=>l.SignedQuantity<0 && l.PutCall is 1 or 2))
        {
            var observation=StrategyPositionLegOptionData.Observe(leg);
            bool Fresh(DateTime? at)=>at is {Kind:DateTimeKind.Utc} && at<=nowUtc && nowUtc-at<=TimeSpan.FromSeconds(maximumAgeSeconds);
            if(!Fresh(observation.QuoteAtUtc) || !Fresh(observation.RiskCalculatedAtUtc)
                || observation.Delta is not {} delta || !double.IsFinite(delta) || Math.Abs(delta)>1 || Math.Abs(delta)<Limit)continue;
            call|=leg.PutCall==1;put|=leg.PutCall==2;
            hits.Add(string.Create(CultureInfo.InvariantCulture,$"Short {(leg.PutCall==1?"call":"put")} {leg.ContractId}: delta {delta:G6}, absolute delta {Math.Abs(delta):G6} >= {Limit:F2}"));
        }
        if(hits.Count==0)return null;
        var side=call&&put?"BOTH_SHORT_LEGS":call?"SHORT_CALL":"SHORT_PUT";
        return new(TradePlanState.ExitRequired,TradePlanAction.ExitAtMarket,true,$"IronCondorTradePlan.{side}_DELTA_LIMIT_REACHED",string.Join("; ",hits)+"; close all remaining condor legs.");
    }
}
