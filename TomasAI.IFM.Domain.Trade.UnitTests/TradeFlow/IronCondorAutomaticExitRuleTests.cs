using Xunit;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;
public sealed class IronCondorAutomaticExitRuleTests
{
    static readonly DateTime Now=new(2026,11,18,18,0,0,DateTimeKind.Utc);
    static StrategyPositionSnapshot Position(double call,double put,int quantity=1)
    {
        var specs=new[]{(right:(byte)1,qty:-quantity,open:8m,delta:call),(right:(byte)1,qty:quantity,open:3m,delta:.95),
            (right:(byte)2,qty:-quantity,open:9m,delta:put),(right:(byte)2,qty:quantity,open:4m,delta:-.95)};
        return new() {Id=StrategyPositionId.Create(new(101,703,1,1),TradeStrategyKind.IronCondor),StrategyKind=TradeStrategyKind.IronCondor,
            Phase=StrategyPositionPhase.MarkToMarket,IsOpen=true,AsOfUtc=Now,ContractCashMultiplier=50,
            Legs=specs.Select((s,i)=>new StrategyPositionLeg {TradeLegId=Guid.NewGuid(),ContractId="OPT"+i,PutCall=s.right,SignedQuantity=s.qty,
                OpeningPrice=s.open,CurrentPrice=s.open,CashMultiplier=50,LastPriceAtUtc=Now,
                Scenario=new() {Delta=s.delta,QuoteAtUtc=Now,RiskCalculatedAtUtc=Now,BidPrice=s.qty<0?6m:3m,AskPrice=s.qty<0?6m:3m}}).ToArray()};
    }
    [Theory]
    [InlineData(.299999,-.299999,false)]
    [InlineData(.30,-.10,true)]
    [InlineData(.10,-.30,true)]
    [InlineData(.40,-.50,true)]
    [InlineData(-.30,.10,true)]
    public void Absolute_short_delta_stop_is_inclusive_and_ignores_high_delta_long_legs(double call,double put,bool exit)
    {
        var p=Position(call,put);
        Assert.Equal(exit,IronCondorShortDeltaExitRule.Evaluate(p,Now,30)?.RequiresExit==true);
    }
    [Theory]
    [InlineData("staleQuote")]
    [InlineData("staleRisk")]
    [InlineData("future")]
    [InlineData("invalid")]
    public void Delta_stop_requires_current_finite_greeks(string kind)
    {
        var p=Position(.40,-.10);var leg=p.Legs[0];var observation=leg.Scenario!;
        observation=kind switch {"staleQuote"=>observation with {QuoteAtUtc=Now.AddSeconds(-31)},"staleRisk"=>observation with {RiskCalculatedAtUtc=Now.AddSeconds(-31)},"future"=>observation with {RiskCalculatedAtUtc=Now.AddSeconds(1)},_=>observation with {Delta=double.NaN}};
        p=p with {Legs=[leg with {Scenario=observation},..p.Legs.Skip(1)]};
        Assert.Null(IronCondorShortDeltaExitRule.Evaluate(p,Now,30));
    }
    [Theory]
    [InlineData(6.00,true)]
    [InlineData(6.01,false)]
    [InlineData(5.99,true)]
    public void Forty_percent_credit_exit_uses_the_executable_debit_and_scales_with_quantity(double debit,bool exit)
    {
        foreach(var quantity in new[]{1,3}) {
            var p=Position(.10,-.10,quantity);
            p=p with {Legs=p.Legs.Select(l=>l with {Scenario=l.Scenario! with {BidPrice=l.SignedQuantity<0?(decimal)debit/2+3:3,AskPrice=l.SignedQuantity<0?(decimal)debit/2+3:3}}).ToArray()};
            Assert.Equal(exit,IronCondorCreditProfitExitRule.Evaluate(p,Now,30)?.RequiresExit==true);
        }
    }
    [Fact]
    public void Qualified_delta_exit_survives_missing_forward_inputs_and_persists_its_reason()
    {
        var p=Position(.10,-.33);var plan=new StrategyTradePlanSnapshot {Position=p,CalculatedAtUtc=Now};
        var updated=UpdateIronCondorTradePlan.ApplyMonitoringRecommendation(plan,new() {Position=p,CalculationStatus="Unavailable",ForwardPriceCalculationError="Missing forward inputs"});
        Assert.True(updated.RequiresExit);Assert.Equal(TradePlanAction.ExitAtMarket,updated.Action);Assert.Contains("SHORT_PUT",updated.ReasonCode);
        Assert.True(updated.IronCondorTradePlanSnapshot!.ExitRecommended);
    }
}
