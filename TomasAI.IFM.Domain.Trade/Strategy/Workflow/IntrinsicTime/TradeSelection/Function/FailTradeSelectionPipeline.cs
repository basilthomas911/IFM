using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function;

/// <summary>Maps contract, calculation, transport and lifecycle failures to non-durable terminal events.</summary>
public static class FailTradeSelectionPipeline
{
    /// <summary>Classifies a failure and builds its event without persisting incomplete state.</summary>
    public static FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent> Fail(
        this FunctionEventContext<ExecuteTradeSelectionPipelineCommand> input, TimeProvider clock)
    {
        if (!input.IsConflict && input.Exception is null) throw new ArgumentException("Failure requires a conflict or exception.");
        var reason = input.IsConflict ? "TS.CONTRACT.CONFLICTING_DUPLICATE"
            : input.Exception is TradeSelectionValidationException validation ? validation.ReasonCode
            : input.Exception is TimeoutException ? "TS.TIME.EXPIRED" : input.Stage switch
            {
                FunctionFailureStage.Loading or FunctionFailureStage.Persistence => "TS.PERSISTENCE.FAILED",
                FunctionFailureStage.Projection => "TS.PROJECTION.FAILED",
                FunctionFailureStage.Execution => "TS.CALCULATION.FAILED",
                _ => "TS.CONTRACT.INVALID"
            };
        if (!input.IsConflict && input.Exception is TomasAI.IFM.Shared.Exceptions.CommandValidationException mapped)
        {
            var code = System.Text.RegularExpressions.Regex.Match(mapped.Message, @"TS\.[A-Z_]+\.[A-Z_]+");
            if (code.Success) reason = code.Value;
        }
        TradeSelectionTelemetry.Failure(reason);
        return FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>.Fail(CreateFailedEvent(input.Request, reason, clock));
    }

    /// <summary>Constructs failure metadata for valid or malformed ingress.</summary>
    static TradeSelectionFunctionFailedEvent CreateFailedEvent(ExecuteTradeSelectionPipelineCommand? c,string reason,TimeProvider clock)
    {
        var now=clock.GetUtcNow().UtcDateTime;
        return new()
        {
            Subject=new(ActorType.Function,ExecuteTradeSelectionPipelineCommand.Actor,TradeSelectionFunctionFailedEvent.Verb,c?.EntityId.Format()??string.Empty),
            Id=Guid.NewGuid(),EntityId=c?.WorkflowEntityId??default,WorkflowId=c?.WorkflowId??default,CommandId=c?.CommandId??Guid.Empty,InputWorkflowRevision=c?.InputWorkflowRevision??0,
            ErrorDate=now,ReceivedOn=now,ErrorCode=TradeSelectionFunctionFailedEvent.ErrorId,ErrorType=ErrorType.Command,ErrorData=reason,ErrorMessage="Trade selection failed: "+reason,
            EventSource=c?.EventSource??ExecuteTradeSelectionPipelineCommand.Actor,AggregateId=c?.EntityId.Format()??string.Empty,CommandName=nameof(ExecuteTradeSelectionPipelineCommand),RouteTo=c?.RouteTo.ToString()??string.Empty,
            CorrelationId=c?.CorrelationId??Guid.Empty,CausationId=c?.CausationId??Guid.Empty,PipelineStage=StrategyWorkflowStage.TradeSelection,ExpiresAtUtc=c?.ExpiresAtUtc??default,
            ReasonCode=reason,InputPayloadSha256=c?.SelectionBinding?.PayloadSha256??string.Empty
        };
    }
}
