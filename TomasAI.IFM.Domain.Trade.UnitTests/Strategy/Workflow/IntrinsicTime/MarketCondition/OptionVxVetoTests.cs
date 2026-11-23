using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using NSubstitute;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Tests.Support;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;
public sealed class OptionVxVetoTests
{
    public static IEnumerable<object[]> Cases()=>OptionVxVetoScenario.Cases();
    [Theory,MemberData(nameof(Cases))]
    public async Task Only_confirmed_backwardation_is_a_market_state_veto(TimeFrameType frame,string scenario)
    {
        var x=await OptionVxVetoScenario.Create(frame,scenario);
        var unavailable=scenario is "missing" or "stale" or "zero" or "duplicate";
        Assert.Equal(unavailable?AssessmentAvailability.Unavailable:AssessmentAvailability.Available,x.Result.Assessment.Availability);
        Assert.Equal(scenario=="backwardation",MarketConditionObservationPolicy.HasOptionVeto(x.Request.WorkflowView,x.Result.Assessment));
        var state=new IntrinsicTimeStrategyWorkflowCommandState();
        state.Apply(new WorkflowStrategyStateUpdatedEvent{State=x.Request.WorkflowView,WorkflowId=x.Request.WorkflowId,
            WorkflowRevision=x.Request.InputWorkflowRevision,EntityId=x.Request.WorkflowEntityId},addEvent:false);
        var command=new CompleteMarketConditionCommand{CommandId=Guid.NewGuid(),EntityId=x.Request.WorkflowEntityId,
            WorkflowId=x.Request.WorkflowId,InputWorkflowRevision=x.Request.InputWorkflowRevision,SourceEventId=x.Result.ResultId,
            Result=StrategyStageResultEnvelope.CreateAssessment(x.Result),CompletedAtUtc=x.Request.RequestedAtUtc,
            Subject=new(ActorType.Command,CompleteMarketConditionCommand.Actor,CompleteMarketConditionCommand.Verb,x.Request.WorkflowEntityId.Format())};
        var context=Substitute.For<IIntrinsicTimeStrategyWorkflowCommandContext>();
        context.TimeProvider.Returns(new Clock(x.Request.RequestedAtUtc));context.Logger.Returns(Substitute.For<ILogger<IntrinsicTimeStrategyWorkflowCommandActor>>());
        Assert.True(command.Execute(context,state).Success);
        var view=state.CurrentView!;
        Assert.Equal(unavailable||scenario=="backwardation"?StrategyWorkflowContinuationDecision.Stop:StrategyWorkflowContinuationDecision.Proceed,view.MarketCondition.ContinuationDecision);
        var tooltip=IntrinsicTimeStrategyUpdatesGridRow.FromView(view).Stages[1].ReasonSentence!;
        if(scenario=="backwardation")
        {
            Assert.Equal(StrategyWorkflowOutcome.NoTrade,view.Outcome);Assert.Equal(MarketConditionObservationPolicy.BackwardationReason,view.StopReasonCode);
            Assert.Equal(StrategyActorProcessingStatus.NotStarted,view.TradeSelection.ProcessingStatus);
            Assert.Contains("backwardation",tooltip);Assert.Contains("22",tooltip);Assert.Contains("21",tooltip);Assert.DoesNotContain("legacy",tooltip);
        }
        else if(unavailable)Assert.Contains("VX",tooltip);
        else Assert.Contains("assessment available",tooltip);
        Assert.True(view.MarketCondition.Result!.HasValidPayloadSha256());
    }
    [Fact]
    public async Task Daily_futures_and_wrong_family_horizons_do_not_inherit_the_veto()
    {
        var x=await OptionVxVetoScenario.Create(TimeFrameType.Monthly,"backwardation");
        var (daily,snapshot)=DailyFuturesGateTestScenario.Create();
        Assert.False(MarketConditionObservationPolicy.IsOptionScope(daily.WorkflowView));
        var result=new MarketConditionAssessmentCalculator().Calculate(daily,snapshot,daily.CommandId);
        Assert.Equal(AssessmentAvailability.Available,result.Assessment.Availability);
        foreach(var pair in new[]{("Future",TimeFrameType.Daily),("IronCondor",TimeFrameType.Weekly),("Vertical",TimeFrameType.Monthly)})
        {
            var entity=x.Request.WorkflowEntityId with{Assignment=x.Request.WorkflowEntityId.Assignment! with{Family=pair.Item1}};
            var view=x.Request.WorkflowView with{EntityId=entity,TriggerEvent=x.Request.TriggerEvent with{EntityId=x.Request.TriggerEvent.EntityId with{TimePeriod=pair.Item2}}};
            Assert.False(MarketConditionObservationPolicy.HasOptionVeto(view,x.Result.Assessment));
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Capture_uses_the_feed_pair_and_original_prices_and_rejects_a_rollover_race(bool race)
    {
        var at=new DateTime(2026,11,19,15,0,0,DateTimeKind.Utc);
        var api=Substitute.For<IMarketDataApi>();
        var front=new FuturesContractV3ReadModel{ContractId="VX20261216",Symbol="VX",LastTradeDate=new(2026,12,16)};
        var second=front with{ContractId="VX20270120",LastTradeDate=new(2027,1,20)};
        var pair=new FuturesTermStructureContracts(front,second);var calls=0;
        api.TryGetFuturesTermStructureContracts("VX",out Arg.Any<FuturesTermStructureContracts>()).Returns(call=>
        {call[1]=race && ++calls>1?new FuturesTermStructureContracts(second,second with{ContractId="VX20270217",LastTradeDate=new(2027,2,17)}):pair;return true;});
        foreach(var contract in new[]{front,second})
        {
            api.GetFuturesMarketHealth(contract.ContractId).Returns(new FuturesMarketHealthSnapshot(true,true,"generation",DateOnly.FromDateTime(at),new(at),1));
            api.TryGetLastTickPrice(contract.ContractId,out Arg.Any<FuturesMarketPriceSnapshot>()).Returns(call=>
            {call[1]=new FuturesMarketPriceSnapshot(contract.ContractId,1,1,default,DateOnly.FromDateTime(at),null,
                new FuturesMarketTradeSnapshot(contract==front?22:21,1,10,new(at.AddSeconds(-1)),new(at)));return true;});
        }
        var result=MarketConditionAssessmentSnapshotProvider.CaptureVxCurve(api,at);
        Assert.All(result,o=>Assert.Equal(race?MarketSourceAvailability.Unavailable:MarketSourceAvailability.Available,o.Availability));
        if(!race){Assert.Equal(22,result[0].Value);Assert.Equal(21,result[1].Value);Assert.Equal(at.AddSeconds(-1),result[0].ObservedAtUtc);Assert.Equal(front.ContractId,result[0].Reason);}
    }
    sealed class Clock(DateTime at):TimeProvider{public override DateTimeOffset GetUtcNow()=>new(at);}
}

