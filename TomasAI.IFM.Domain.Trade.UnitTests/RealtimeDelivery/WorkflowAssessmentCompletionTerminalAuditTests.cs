using MessagePack;
using NSubstitute;
using TomasAI.IFM.TestSupport;
using TomasAI.IFM.Tests.Support;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Runtime;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime;

namespace TomasAI.IFM.Domain.Trade.UnitTests.RealtimeDelivery;
public sealed class WorkflowAssessmentCompletionTerminalAuditTests
{
    [Fact]
    public async Task Cached_assessment_completion_preserves_time_after_accepted_but_lost_reply()
    {
        var (command,snapshot)=DailyFuturesGateTestScenario.Create("crossed");
        var view=command.WorkflowView with{DecisionRuntimeVersion=1};
        var initial=WorkflowDispatchDeliveryFixture.AssessmentFunctionInput();
        var source=initial with{EntityId=view.EntityId,WorkflowId=view.WorkflowId,WorkflowRevision=view.WorkflowRevision,UpdatedAtUtc=view.UpdatedAtUtc,State=view};
        using var journal=new WorkflowPublicationJournal();
        await using var state=new WorkflowDispatchDeliveryFixture(publications:journal);state.Time.At=command.RequestedAtUtc;
        var provider=Substitute.For<IMarketConditionAssessmentSnapshotProvider>();state.Context.PreparedMarketContext.Returns(provider);
        provider.CaptureDailyFuturesMarketAsync(Arg.Any<MarketConditionAssessmentParameterSet>(),Arg.Any<DateTime>(),Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(snapshot));
        await using var executor=new WorkflowDecisionExecutor(timeProvider:state.Time);state.Context.DecisionRuntime.Returns(new WorkflowCalculationRuntime(executor,state.Time));
        byte[]? accepted=null;var conflict=false;
        state.Send=message=>
        {
            var terminal=Assert.IsType<CompleteMarketConditionCommand>(message);var bytes=MessagePackSerializer.Serialize(terminal);
            if(accepted is null){accepted=bytes;return ValueTask.FromException(new IOException("Assessment completion accepted; reply lost."));}
            conflict=!accepted.AsSpan().SequenceEqual(bytes);
            return conflict?ValueTask.FromException(new InvalidDataException("Same cached assessment changed its terminal timestamp.")):ValueTask.CompletedTask;
        };
        await state.Start();Assert.False(await state.Deliver(source));state.Time.At=state.Time.At.AddSeconds(1);
        Assert.True(await state.Deliver(source));Assert.False(conflict);Assert.Equal(2,state.Commands.Count);
        var first=Assert.IsType<CompleteMarketConditionCommand>(state.Commands[0]);var retry=Assert.IsType<CompleteMarketConditionCommand>(state.Commands[1]);
        Assert.Equal(first.CommandId,retry.CommandId);Assert.Equal(first.SourceEventId,retry.SourceEventId);Assert.Equal(first.CompletedAtUtc,retry.CompletedAtUtc);
        Assert.Equal(MessagePackSerializer.Serialize(first.Result),MessagePackSerializer.Serialize(retry.Result));
        Assert.Null(first.TradeSelectionInitializationFailure);Assert.Null(retry.TradeSelectionInitializationFailure);
    }
}
