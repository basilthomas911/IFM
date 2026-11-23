using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using Xunit;
using NSubstitute;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.State;
using TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;
public sealed class IronCondorClosePlanTests
{
    [Fact]
    public async Task Replayed_exit_decision_prevents_later_market_marks_from_creating_another_decision_plan()
    {
        var decision=Decision();
        var state=new IronCondorTradePlanFunctionState();
        state.ReplayEvents([new IronCondorTradePlanUpdatedEvent {Id=Guid.NewGuid(),Plan=decision}]);
        var command=new UpdateIronCondorTradePlanCommand {Position=decision.Position,RequestedAtUtc=Now.AddSeconds(1)};
        await Assert.ThrowsAsync<SupersededIronCondorPlanObservationException>(async()=>
            await command.ExecuteAsync(state,Substitute.For<IIronCondorTradePlanFunctionContext>(),_=>throw new InvalidOperationException("No new decision may be dispatched"),default));
    }
    static readonly DateTime Now = new(2026,11,18,18,0,0,DateTimeKind.Utc);
    static StrategyTradePlanSnapshot Decision() => new() {
        Position = new() { Id=StrategyPositionId.Create(new(101,703,1,1),TradeStrategyKind.IronCondor),IsOpen=true,Phase=StrategyPositionPhase.MarkToMarket,
            ContractCashMultiplier=50,OpeningCommission=2,Legs=Enumerable.Range(0,4).Select(i=>new StrategyPositionLeg {TradeLegId=Guid.NewGuid(),ContractId="OPT"+i,SignedQuantity=i%2==0?-1:1}).ToArray() },
        State=TradePlanState.ExitRequired,RequiresExit=true,Action=TradePlanAction.ExitAtMarket,
        CalculatedAtUtc=Now,ReasonCode="SHORT_PUT_DELTA_LIMIT_REACHED",Explanation="Short put delta -0.3265 reached absolute limit 0.30.",
        IronCondorTradePlanSnapshot=new() {ActionReason="Original decision",CalculationStatus="Complete"}
    };
    [Theory]
    [InlineData("Submitted")]
    [InlineData("PartiallyFilled 1/3")]
    [InlineData("Rejected")]
    [InlineData("Cancelled")]
    public void Execution_progress_preserves_decision_and_does_not_close_an_open_position(string status)
    {
        var source=Decision();
        var plan=IronCondorClosePlan.Create(source,source.Position,status,2,Now.AddMinutes(1));
        Assert.True(plan.RequiresExit);Assert.Equal(TradePlanState.ExitRequired,plan.State);
        Assert.Equal(source.ReasonCode,plan.ReasonCode);Assert.Contains(source.Explanation,plan.Explanation);
        Assert.Contains(status,plan.Explanation);Assert.Contains("remains open",plan.Explanation);
        Assert.Equal("Closing",plan.IronCondorTradePlanSnapshot!.ActionState);
        Assert.Equal(source.ForwardTradePrice,plan.ForwardTradePrice);
    }
    [Fact]
    public void Final_plan_requires_zero_remaining_legs_and_confirmed_fills_and_records_cash_pnl()
    {
        var source=Decision();
        var position=source.Position with {IsOpen=false,Phase=StrategyPositionPhase.Close,AsOfUtc=Now.AddMinutes(2),RealizedPnl=-2,
            Legs=source.Position.Legs.Select(l=>l with {SignedQuantity=0}).ToArray(),ClosingFills=[new() {Commission=3}]};
        Assert.True(IronCondorClosePlan.IsSettled(position));
        Assert.False(IronCondorClosePlan.IsSettled(position with {ClosingFills=[]}));
        var plan=IronCondorClosePlan.Create(source,position,"Filled 1/1; settlement confirmed",3,Now.AddMinutes(2));
        Assert.Equal(TradePlanState.Closed,plan.State);Assert.False(plan.RequiresExit);Assert.Equal(TradePlanAction.None,plan.Action);
        Assert.Equal(-105,plan.IronCondorTradePlanSnapshot!.TradePnl);Assert.Contains("closed position confirmed",plan.Explanation);
        Assert.Equal(plan.Explanation,plan.IronCondorTradePlanSnapshot.ActionReason);
        Assert.Equal(0,plan.IronCondorTradePlanSnapshot.NetDelta);
        Assert.Equal(0,plan.IronCondorTradePlanSnapshot.CurrentPositionValue);
        Assert.Equal(plan.CalculatedAtUtc,plan.IronCondorTradePlanSnapshot.ActionDateTime);
    }
}
