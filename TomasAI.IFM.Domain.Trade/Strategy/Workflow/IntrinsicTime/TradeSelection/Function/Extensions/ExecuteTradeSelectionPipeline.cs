using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Extensions;

public static class ExecuteTradeSelectionPipeline
{
    public static ValueTask<FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>> ExecuteAsync(
        this ExecuteTradeSelectionPipelineCommand c, ITradeSelectionFunctionContext context, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var now=context.TimeProvider.GetUtcNow().UtcDateTime;
        if(now>=c.ExpiresAtUtc) throw new TimeoutException();
        var result=TradeSelectionEvaluator.Evaluate(c);
        var policy=TradeSelectionContracts.CommonPolicy(c.SelectionBinding);
        TradeSelectionContracts.Require(c.EvaluatedAtUtc<=now.AddSeconds(policy.FutureClockSkewSeconds),"TS.TIME.FUTURE","Evaluation timestamp is in the future.");
        token.ThrowIfCancellationRequested();
        var completed=new TradeSelectionFunctionCompletedEvent
        {
            Subject=new(ActorType.Function,ExecuteTradeSelectionPipelineCommand.Actor,TradeSelectionFunctionCompletedEvent.Verb,c.EntityId.Format()),
            Id=result.ResultId,EntityId=c.WorkflowEntityId,CommandId=c.CommandId,AggregateId=c.EntityId.Format(),EventSource=c.EventSource,ReceivedOn=result.ProducedAtUtc,
            WorkflowId=c.WorkflowId,InputWorkflowRevision=c.InputWorkflowRevision,CorrelationId=c.CorrelationId,CausationId=c.CausationId,PipelineStage=StrategyWorkflowStage.TradeSelection,
            Result=StrategyStageResultEnvelope.Create(result.ResultId,nameof(TradeSelectionResult),1,MessagePackSerializer.Serialize(result),c.AssessmentResultEnvelope.MarketDataAsOfUtc,result.ProducedAtUtc,maximumPayloadBytes:policy.MaximumResultPayloadBytes),
            CompletedAtUtc=result.ProducedAtUtc,ExpiresAtUtc=c.ExpiresAtUtc,ParameterPayloadSha256=c.SelectionBinding.CommonPolicy.PayloadSha256,
            EvaluatedAtUtc=result.EvaluatedAtUtc,ValidUntilUtc=result.ValidUntilUtc,RequestFingerprint=c.Fingerprint()
        };
        return ValueTask.FromResult(FunctionResult<TradeSelectionFunctionCompletedEvent,TradeSelectionFunctionFailedEvent>.Complete(completed));
    }
    public static TradeSelectionFunctionFailedEvent CreateFailedEvent(ExecuteTradeSelectionPipelineCommand? c,string reason,TimeProvider clock)
    {
        var now=clock.GetUtcNow().UtcDateTime;
        return new()
        {
            Subject=new(ActorType.Function,ExecuteTradeSelectionPipelineCommand.Actor,TradeSelectionFunctionFailedEvent.Verb,c?.EntityId.Format()??string.Empty),
            Id=Guid.NewGuid(),EntityId=c?.WorkflowEntityId??default,WorkflowId=c?.WorkflowId??default,CommandId=c?.CommandId??Guid.Empty,InputWorkflowRevision=c?.InputWorkflowRevision??0,
            ErrorDate=now,ReceivedOn=now,ErrorCode=TradeSelectionFunctionFailedEvent.ErrorId,ErrorType=ErrorType.Command,ErrorData=reason,ErrorMessage="Trade selection failed: "+reason,
            EventSource=c?.EventSource??ExecuteTradeSelectionPipelineCommand.Actor,AggregateId=c?.EntityId.Format()??string.Empty,CommandName=nameof(ExecuteTradeSelectionPipelineCommand),RouteTo=c?.RouteTo.ToString()??string.Empty,
            CorrelationId=c?.CorrelationId??Guid.Empty,CausationId=c?.CausationId??Guid.Empty,PipelineStage=StrategyWorkflowStage.TradeSelection,ExpiresAtUtc=c?.ExpiresAtUtc??default,
            ReasonCode=reason,InputPayloadSha256=c?.SelectionBinding.PayloadSha256??string.Empty
        };
    }
}
