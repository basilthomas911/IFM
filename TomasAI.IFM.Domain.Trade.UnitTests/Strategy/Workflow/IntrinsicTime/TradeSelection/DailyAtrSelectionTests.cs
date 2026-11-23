using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using Xunit;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
public sealed class DailyAtrSelectionTests
{
    [Theory]
    [InlineData("LongFuture", 1, IntrinsicTimeModeType.Trending)]
    [InlineData("LongFuture", 1, IntrinsicTimeModeType.TrendExtremeChanged)]
    [InlineData("LongFuture", 1, IntrinsicTimeModeType.TrendOvershootExtended)]
    [InlineData("ShortFuture", -1, IntrinsicTimeModeType.Trending)]
    [InlineData("ShortFuture", -1, IntrinsicTimeModeType.TrendExtremeChanged)]
    [InlineData("ShortFuture", -1, IntrinsicTimeModeType.TrendOvershootExtended)]
    public async Task Real_evaluator_selects_only_after_quarter_ATR_and_serialization_preserves_hash(string variant,int sign,IntrinsicTimeModeType mode)
    {
        var at=new DateTime(2026,11,19,15,0,1,DateTimeKind.Utc);
        var c=await TradeSelectionFixture.Command(variant,atUtc:at);
        var signal=c.TriggerEvent.FuturesItiSignal! with {IntrinsicTimeMode=mode,IntrinsicTime=at,IntrinsicPrice=100+sign*3};
        var trigger=c.TriggerEvent with{FuturesItiSignal=signal};
        var arm=new DailyFuturesEntryState{DcTriggerId=Guid.NewGuid(),ContractId=signal.ContractId,ValueDate=signal.ValueDate,
            DcAtUtc=at.AddSeconds(-1),DcPrice=100,Threshold=8,Atr=new(signal.ContractId,8,at.AddMinutes(-1),at.AddSeconds(-30),1)};
        c=c with{TriggerEvent=trigger,WorkflowView=c.WorkflowView with{TriggerEvent=trigger,DailyFuturesEntry=arm}};
        var selected=TradeSelectionEvaluator.Evaluate(c);
        Assert.Equal(SelectionOutcome.Selected,selected.Outcome);
        Assert.Equal(TradeSelectionContracts.WireHash(selected),TradeSelectionContracts.WireHash(TradeSelectionContracts.ReadResult(StrategyStageResultEnvelope.CreateSelection(selected,524288))));
        var completed=await new TradeSelectionFunctionTests.FunctionFixture(c).Execute();
        Assert.True(completed.IsCompleted,completed.Failed?.ErrorMessage);
        var restored=MessagePackSerializer.Deserialize<Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands.ExecuteTradeSelectionPipelineCommand>(MessagePackSerializer.Serialize(c));
        Assert.Equal(c.Fingerprint(),restored.Fingerprint());
        Assert.Equal(TradeSelectionContracts.WireHash(selected),TradeSelectionContracts.WireHash(TradeSelectionEvaluator.Evaluate(restored)));
        var consumed=c with{WorkflowView=c.WorkflowView with{DailyFuturesEntry=arm with{Consumed=true}}};
        var refusal=TradeSelectionEvaluator.Evaluate(consumed);
        Assert.Equal("TS.DAILY.DC_CONSUMED",TradeSelectionContracts.ReadResult(StrategyStageResultEnvelope.CreateSelection(refusal,524288)).PrimaryReasonCode);
        signal=signal with{IntrinsicPrice=100+sign*2};trigger=trigger with{FuturesItiSignal=signal};
        var boundary=c with{TriggerEvent=trigger,WorkflowView=c.WorkflowView with{TriggerEvent=trigger}};
        Assert.Equal("TS.DAILY.ENTRY_DISTANCE",TradeSelectionEvaluator.Evaluate(boundary).PrimaryReasonCode);
    }
}
