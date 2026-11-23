using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command;
using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Futures.Position.Plan.Model;
using TomasAI.IFM.Domain.Trade.Futures.Position.Realtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Tests.Support;
using Xunit;
using S = TomasAI.IFM.Tests.Support.DailyAtrFuturesScenario;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;
public sealed class DailyAtrFuturesTests
{
    [Theory]
    [InlineData(101.75,RegimeDirection.Up,false)]
    [InlineData(102,RegimeDirection.Up,false)]
    [InlineData(102.25,RegimeDirection.Up,true)]
    [InlineData(98,RegimeDirection.Down,false)]
    [InlineData(97.75,RegimeDirection.Down,true)]
    [InlineData(103,RegimeDirection.Down,false)]
    [InlineData(103,RegimeDirection.Neutral,false)]
    public void Entry_uses_strict_distance_and_EMA_direction(double price,RegimeDirection direction,bool eligible)
        => Assert.Equal(eligible,DailyFuturesEntryRules.Rejection(S.View(price),direction,S.At.AddSeconds(1)) is null);

    [Theory]
    [InlineData(IntrinsicTimeModeType.TrendDirectionChanged,"TS.DAILY.ARMED_WAIT")]
    [InlineData(IntrinsicTimeModeType.TrendReversalChanged,"TS.DAILY.NOT_CONTINUATION")]
    [InlineData(IntrinsicTimeModeType.TrendOvershootReversal,"TS.DAILY.NOT_CONTINUATION")]
    [InlineData(IntrinsicTimeModeType.HoldTradeChanged,"TS.DAILY.NOT_CONTINUATION")]
    public void DC_arms_and_non_continuation_events_do_not_enter(IntrinsicTimeModeType mode,string expected)
        => Assert.Equal(expected,DailyFuturesEntryRules.Rejection(S.View(103,mode),RegimeDirection.Up,S.At.AddSeconds(1)));

    [Fact]
    public void Actual_downtrend_new_low_can_trigger_the_armed_short_without_reclassifying_the_signal()
    {
        var state=new TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State.FuturesItiSignalCommandState();
        TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events.FuturesItiSignalGeneratedEvent Step(double price,int seconds)
        {
            var cmd=new TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands.GenerateFuturesItiSignalCommand(S.Contract,S.Date,TimeFrameType.Daily,S.At.AddSeconds(seconds),price,20)
                {RequireDailyAtr=true,DailyAtr=S.Arm.Atr};
            var result=cmd.Execute(state);
            Assert.True(result.Success,result.ErrorMessage);
            // The actor persistence boundary stamps the committed event identity.
            return state.Events.OfType<TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events.FuturesItiSignalGeneratedEvent>().Last()
                with {Id=Guid.NewGuid(),CreatedOn=cmd.Timestamp};
        }
        Step(100,0);
        var dc=Step(92,1);
        Assert.Equal(IntrinsicTimeModeType.TrendDirectionChanged,dc.FuturesItiSignal.IntrinsicTimeMode);
        var view=S.View() with{TriggerEvent=dc,TriggerEventId=dc.Id};
        var arm=DailyFuturesEntryRules.Advance(null,view,false);
        var low=Step(89,2);
        Assert.Equal(IntrinsicTimeModeType.TrendExtremeChanged,low.FuturesItiSignal.IntrinsicTimeMode);
        view=view with{TriggerEvent=low,TriggerEventId=low.Id,DailyFuturesEntry=arm};
        Assert.Null(DailyFuturesEntryRules.Rejection(view,RegimeDirection.Down,S.At.AddSeconds(2)));
    }

    [Fact]
    public void New_DC_rearms_and_serialized_arm_keeps_exact_ATR_and_consumption()
    {
        var v=S.View(110,IntrinsicTimeModeType.TrendDirectionChanged);
        var arm=DailyFuturesEntryRules.Advance(S.Arm with {Consumed=true},v,false)!;
        Assert.False(arm.Consumed); Assert.Equal(110,arm.DcPrice); Assert.Equal(v.TriggerEventId,arm.DcTriggerId);
        var restored=MessagePackSerializer.Deserialize<DailyFuturesEntryState>(MessagePackSerializer.Serialize(arm with{Consumed=true}));
        Assert.Equal(arm with{Consumed=true},restored);
        Assert.Equal("TS.DAILY.DC_CONSUMED",DailyFuturesEntryRules.Rejection(S.View() with{DailyFuturesEntry=restored},RegimeDirection.Up,S.At.AddSeconds(1)));
        Assert.Equal(Guid.Empty,DailyFuturesEntryRules.Advance(S.Arm,v,true)!.DcTriggerId);
    }

    [Theory]
    [InlineData(1,103,104,95,100)]
    [InlineData(-1,97,96,105,100)]
    public void Tightens_from_DC_at_half_threshold_then_never_loosens(int sign,int first,int reached,int initialStop,int tightStop)
    {
        var p=S.Position(sign,first); var parameters=new TradePlanParameters();
        var a=FuturesTrailingStopModel.Calculate(p,parameters,null,S.At.AddSeconds(1));
        // Initial stop trails the fill, not the DC price.
        Assert.Equal(initialStop,a.StopPrice); Assert.False(a.Tightened);
        var previous=new StrategyTradePlanSnapshot{Position=p,FuturesTrailingExtremePrice=a.ExtremePrice,FuturesStopPrice=a.StopPrice};
        var b=FuturesTrailingStopModel.Calculate(p with{Legs=[p.Legs[0] with{CurrentPrice=reached}]},parameters,previous,S.At.AddSeconds(1));
        Assert.True(b.Tightened);Assert.Equal(tightStop,b.StopPrice);
        previous=MessagePackSerializer.Deserialize<StrategyTradePlanSnapshot>(MessagePackSerializer.Serialize(previous with
            {FuturesTrailingExtremePrice=b.ExtremePrice,FuturesStopPrice=b.StopPrice,FuturesTrailTightened=true}));
        var c=FuturesTrailingStopModel.Calculate(p,parameters,previous,S.At.AddSeconds(1));
        Assert.True(c.Tightened);Assert.Equal(tightStop,c.StopPrice);Assert.False(c.Reached);
    }

    [Theory]
    [InlineData(1,104,100)]
    [InlineData(-1,96,100)]
    public void Stop_touch_requires_existing_exit_workflow_action_and_freezes_decision(int sign,int best,int stopped)
    {
        var algorithm=new FuturesTradePlanAlgorithm();var parameters=new TradePlanParameters{MaximumLoss=1,WarningLoss=1,ProfitTarget=1};
        var p=S.Position(sign,best) with{UnrealizedPnl=1000};
        var first=algorithm.Calculate(p,parameters,null,S.Date,S.At.AddSeconds(1)).Snapshot;
        Assert.False(first.RequiresExit); // The legacy dollar profit target is not an exit for this policy.
        p=p with{PositionSequence=2,Legs=[p.Legs[0] with{CurrentPrice=stopped}]};
        var exit=algorithm.Calculate(p,parameters,first,S.Date,S.At.AddSeconds(1)).Snapshot;
        Assert.True(exit.RequiresExit);Assert.Equal(TradePlanAction.ExitAtMarket,exit.Action);Assert.Equal("PLAN.FUTURES.TRAILING_STOP",exit.ReasonCode);
        var retried=algorithm.Calculate(p with{Legs=[p.Legs[0] with{CurrentPrice=best}]},parameters,exit,S.Date,S.At.AddSeconds(1)).Snapshot;
        Assert.True(retried.RequiresExit);Assert.Equal(exit.FuturesStopPrice,retried.FuturesStopPrice);
    }

    [Fact]
    public async Task Repeated_stop_and_clock_results_address_one_existing_exit_workflow()
    {
        var context=NSubstitute.Substitute.For<TomasAI.IFM.Domain.Trade.Futures.Position.Realtime.Actor.IFuturesTradePositionRealtimeContext>();
        context.TimeProvider.Returns(TimeProvider.System);
        context.Parameters.Returns(new TradePlanParameters());
        var p=S.Position();
        var plan=new FuturesTradePlanAlgorithm().Calculate(p,new(),null,S.Date,DailyFuturesSession.FlattenUtc(S.Date)).Snapshot;
        context.RequestFunctionAsync<UpdateFuturesTradePlanCommand,FuturesTradePlanId,
            TomasAI.IFM.Shared.EventSourcing.FunctionResult<FuturesTradePlanUpdatedEvent,TradePlanFailedEvent<FuturesTradePlanId>>>(
            Arg.Any<UpdateFuturesTradePlanCommand>(),Arg.Any<CancellationToken>()).Returns(call=>
                new TomasAI.IFM.Shared.EventSourcing.ServiceOk<TomasAI.IFM.Shared.EventSourcing.FunctionResult<FuturesTradePlanUpdatedEvent,TradePlanFailedEvent<FuturesTradePlanId>>>(
                    new(new FuturesTradePlanUpdatedEvent{Id=Guid.NewGuid(),Plan=plan},null)));
        for(var i=0;i<3;i++)
            await new TomasAI.IFM.Domain.Trade.Shared.Futures.Position.FuturesPositionChangedEvent
                {Id=Guid.NewGuid(),PositionSnapshot=p with{PositionSequence=i+1}}.ExecuteAsync(context);
        await DailyFuturesFlattenService.Create(p,DailyFuturesSession.FlattenUtc(S.Date))!.ExecuteAsync(context);
        var sends=context.ReceivedCalls().Where(c=>c.GetMethodInfo().Name=="SendAsync")
            .Select(c=>Assert.IsType<TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow.StartFuturesExitPositionWorkflowCommand>(c.GetArguments()[0])).ToArray();
        Assert.Equal(4,sends.Length);Assert.Single(sends.Select(x=>x.EntityId).Distinct());Assert.Single(sends.Select(x=>x.CommandId).Distinct());
        Assert.All(sends,x=>Assert.True(x.ExitPlan.Plan.RequiresExit));
    }

    [Fact]
    public void Filled_futures_carry_the_frozen_DC_and_ATR_through_order_evidence()
    {
        var view=S.View() with{DailyFuturesEntry=S.Arm with{Consumed=true}};
        var request=new Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.ExecuteOrderCompositionPipelineCommand{WorkflowView=view};
        var evidence=TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketDecisionEvidence.Capture(request,S.At);
        Assert.True(evidence.IsValid);
        var component=Guid.NewGuid();var leg=Guid.NewGuid();
        var execution=new OrderExecutionDefinition{TradeOrderId=new(801,701,1601),ExecutionAttemptId=Guid.NewGuid(),Status=OrderExecutionStatus.PartiallyFilled,
            Order=new TradeOrderDefinition{DecisionEvidence=evidence},
            Components=[new(){ComponentId=component,ReservedTradeId=123,StrategyKind=TradeStrategyKind.FuturesOutright,
                Legs=[new(){TradeLegId=leg,ContractId=S.Contract,SignedQuantity=1}]}],
            Fills=[new(){ComponentId=component,TradeLegId=leg,ExecutionFillId=Guid.NewGuid(),SignedQuantity=1,Price=103}]};
        var result=TomasAI.IFM.Domain.Trade.Order.Execution.Command.Model.OrderExecutionComputation.Accept(execution,S.At.AddSeconds(1));
        Assert.True(result.Accepted,result.RejectionReason);
        var trade=Assert.Single(result.CreatedTrades);
        Assert.Equal(view.DailyFuturesEntry,trade.DailyFuturesEntry);
        var restored=MessagePackSerializer.Deserialize<EstablishedTradeDefinition>(MessagePackSerializer.Serialize(trade));
        Assert.Equal(view.DailyFuturesEntry,restored.DailyFuturesEntry);
    }

    [Fact]
    public void Flatten_clock_preserves_no_tick_times_and_works_after_restart()
    {
        var p=S.Position();var cutoff=DailyFuturesSession.FlattenUtc(S.Date);
        Assert.Null(DailyFuturesFlattenService.Create(p,cutoff.AddTicks(-1)));
        var clock=DailyFuturesFlattenService.Create(p,cutoff)!;
        Assert.Same(p,clock.PositionSnapshot);Assert.Equal(p.LastMarketObservationUtc,clock.State.LastMarketObservationUtc);
        var restored=MessagePackSerializer.Deserialize<StrategyPositionSnapshot>(MessagePackSerializer.Serialize(p));
        Assert.Equal(clock.Id,DailyFuturesFlattenService.Create(restored,cutoff.AddMinutes(1))!.Id);
        var plan=new FuturesTradePlanAlgorithm().Calculate(restored,new(),null,S.Date,cutoff).Snapshot;
        Assert.Equal("PLAN.DAILY.SESSION_FLATTEN",plan.ReasonCode);Assert.True(plan.RequiresExit);
    }
}
