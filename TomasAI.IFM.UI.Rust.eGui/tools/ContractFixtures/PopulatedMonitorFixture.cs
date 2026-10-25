using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondor.ForwardPrice;

/// <summary>Desktop transport/display evidence using synthetic quoted risks.
/// Marks and cash P&L are internally consistent, before fees. This is not a
/// calibration of option prices, Greeks, or the forward-risk algorithm.</summary>
internal static class PopulatedMonitorFixture
{
    internal static EstablishedTradeDefinition Trade => MonitorFixtures.Trade with {
        OpeningHistoryValuation=new(MonitorFixtures.Date,4,1,-14m,0m),
        OpeningPositionSnapshot=MonitorFixtures.Trade.OpeningPositionSnapshot! with {HistoryValuation=new(MonitorFixtures.Date,4,1,-14m,0m)}
    };

    internal static TradeRealtimeSnapshot Create(bool later=false,bool gain=false)
    {
        var at=new DateTimeOffset(StrategyFixtures.At);
        var chain=ComposerOptionFixtures.Chain().Contracts.ToDictionary(x=>x.ContractId);
        var legs=Trade.Legs.Select(definition=> {
            var input=chain[definition.ContractId];
            var opening=(input.Bid!.Value+input.Ask!.Value)/2;
            var mark=opening+(definition.SignedQuantity<0?(gain?-.5m:later?1m:.5m):0m);
            var quote=new FuturesOptionTickDataV2ReadModel {
                ContractId=definition.ContractId,ValueDate=MonitorFixtures.Date,TickId=later?11:10,
                OptionPrice=(double)mark,BidPrice=(double)(mark-.5m),AskPrice=(double)(mark+.5m),BidSize=1,AskSize=2,
                ImpliedVolatility=.2,UnderlyingPrice=6000,Delta=input.Delta!.Value,Gamma=.001,Vega=.3,Theta=-.2,Rho=0,
                GreeksAvailable=true,TheoreticalPrice=(double)mark,SourceGeneration=Guid.Parse("61112233-4455-6677-8899-aabbccddeeff"),
                ObservedAtUtc=at,CalculatedAtUtc=at,ValidUntilUtc=at.AddMinutes(5),
                PricingInputs=new(){Currency="USD",Multiplier=50,DaysPerYear=365,ExpirationUtc=at.AddDays(4),ModelVersion="SyntheticQuotedRisk/DisplayOnly"}
            };
            return new StrategyPositionLeg {TradeLegId=definition.TradeLegId,ContractId=definition.ContractId,
                SignedQuantity=definition.SignedQuantity,AssetFamily=definition.AssetFamily,Strike=definition.Strike,PutCall=definition.PutCall,
                Expiry=definition.Expiry,OpeningPrice=opening,CurrentPrice=mark,CashMultiplier=50,OptionTick=quote,OptionRisk=quote};
        }).ToArray();
        var value=legs.Sum(x=>x.CurrentPrice*x.SignedQuantity*50);
        var pnl=legs.Sum(x=>(x.CurrentPrice-x.OpeningPrice)*x.SignedQuantity*50);
        if(value!=(gain?-650m:later?-800m:-750m)||pnl!=(gain?50m:later?-100m:-50m))throw new InvalidDataException("Synthetic monitor valuation is inconsistent");
        var position=MonitorFixtures.Mark with {Legs=legs,MarketValue=value,UnrealizedPnl=pnl,DailyPnl=pnl,
            PositionSequence=gain?12:later?11:10,IsOpen=true,HistoryValuation=new(MonitorFixtures.Date,4,1,value/50,0)};
        var forward=gain?14m:later?17m:16m;
        // Exercise the real backend entry-credit/currency scaling. Only the
        // projected closing-cost input is synthetic; do not invent its ratio.
        var risk=IronCondorForwardPriceCalculation.CalculateRisk(new IronCondorForwardPriceCalculatedEvent {
            IronCondorForwardPrice=new(){ForwardPrice=forward,PutForwardPricePoints=forward/2,CallForwardPricePoints=forward/2,
                ContractMultiplier=50,StrategyQuantity=1,Currency="USD",PositionSequence=position.PositionSequence,
                RouteGeneration=position.RouteGeneration,CalculatedAtUtc=at.UtcDateTime}
        },14,1000);
        var plan=MonitorFixtures.Plan with {Position=position,PlanRevision=gain?9:later?8:7,IronCondorTradePlanSnapshot=MonitorFixtures.Plan.IronCondorTradePlanSnapshot! with {
            Position=position,SequenceId=gain?9:later?8:7,CalculationStatus="Complete",TradePnl=pnl,DailyPnl=pnl,NetPrice=value/50,
            MaxProfit=700,MaxLoss=1800,DailyLossLimit=1000,DailyLoss=Math.Max(0,-pnl),LossHeadroom=1000-Math.Max(0,-pnl),
            ForwardPrice=forward,CombinedForwardPrice=forward,CombinedSpreadPrice=value/50,CurrentPositionValue=value,
            CalculationVersion=IronCondorForwardRisk.EntryCreditCalculationVersion,IronCondorForwardRisk=risk,
            ForwardDailyPnl=null,ForwardLoss=null,PutForwardPrice=forward/2*50,CallForwardPrice=forward/2*50,AssetPrice=6000,UnderlyingPrice=6000,
            PutOTMProbability=.84,CallOTMProbability=.84,ForwardLossRatio=risk.ForwardLossRatio,ShortPutGamma=.001,ShortCallGamma=.001,
            RiskValidUntilUtc=at.AddMinutes(5).UtcDateTime,Quantity=1,ContractMultiplier=50,Currency="USD"
        }};
        var cache=new TradeRealtimeHotCache(Trade,MonitorFixtures.Date,new Clock(at));
        cache.Apply(position);foreach(var leg in legs)cache.Apply(leg.OptionTick!);cache.Apply(plan);
        var snapshot=cache.Read();
        if(snapshot.Status!="Available"||snapshot.PlanStatus!="Available"||snapshot.Summary.PositionValue!=value)
            throw new InvalidDataException("Populated monitor did not qualify through the backend hot cache");
        return snapshot with {MonitoringGeneration=MonitorFixtures.Realtime.MonitoringGeneration,SnapshotRevision=gain?22:later?21:20,PublishedAtUtc=at.AddSeconds(gain?2:later?1:0)};
    }
    sealed class Clock(DateTimeOffset value):TimeProvider {public override DateTimeOffset GetUtcNow()=>value;}
}
