using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function;

/// <summary>Maps contract, calculation, transport and lifecycle failures to non-durable terminal events.</summary>
public static class FailOrderCompositionPipeline
{
    /// <summary>Classifies a failure and builds its event without persisting incomplete state.</summary>
    public static FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent> Fail(
        this FunctionEventContext<ExecuteOrderCompositionPipelineCommand> input, TimeProvider clock)
    {
        if (!input.IsConflict && input.Exception is null) throw new ArgumentException("Failure requires a conflict or exception.");
        var reason = input.IsConflict ? "OC.CONTRACT.CONFLICTING_DUPLICATE"
            : input.Exception is CompositionException validation ? validation.ReasonCode
            : input.Exception is TimeoutException ? "OC.TIME.EXPIRED" : input.Stage switch
            {
                FunctionFailureStage.Loading or FunctionFailureStage.Persistence => "OC.PERSISTENCE.FAILED",
                FunctionFailureStage.Projection => "OC.PROJECTION.FAILED",
                FunctionFailureStage.Execution => "OC.CALCULATION.FAILED",
                _ => "OC.CONTRACT.INVALID"
            };
        if (!input.IsConflict && input.Exception is TomasAI.IFM.Shared.Exceptions.CommandValidationException mapped)
        {
            var code = System.Text.RegularExpressions.Regex.Match(mapped.Message, @"OC\.[A-Z_]+\.[A-Z_]+");
            if (code.Success) reason = code.Value;
        }
        OrderCompositionTelemetry.Failure(reason);
        return FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>.Fail(CreateFailedEvent(input.Request, reason, clock));
    }

    /// <summary>Constructs failure metadata for valid or malformed ingress.</summary>
    static OrderCompositionFunctionFailedEvent CreateFailedEvent(ExecuteOrderCompositionPipelineCommand? c,string reason,TimeProvider clock)
    {
        var now=clock.GetUtcNow().UtcDateTime;
        return new()
        {
            Subject=new(ActorType.Function,ExecuteOrderCompositionPipelineCommand.Actor,OrderCompositionFunctionFailedEvent.Verb,c?.EntityId.Format()??string.Empty),
            Id=Guid.NewGuid(),EntityId=c?.WorkflowEntityId??default,WorkflowId=c?.WorkflowId??default,CommandId=c?.CommandId??Guid.Empty,InputWorkflowRevision=c?.InputWorkflowRevision??0,
            ErrorDate=now,ReceivedOn=now,ErrorCode=OrderCompositionFunctionFailedEvent.ErrorId,ErrorType=ErrorType.Command,ErrorData=reason,ErrorMessage="Order composition failed: "+reason,
            EventSource=c?.EventSource??ExecuteOrderCompositionPipelineCommand.Actor,AggregateId=c?.EntityId.Format()??string.Empty,CommandName=nameof(ExecuteOrderCompositionPipelineCommand),RouteTo=c?.RouteTo.ToString()??string.Empty,
            CorrelationId=c?.CorrelationId??Guid.Empty,CausationId=c?.CausationId??Guid.Empty,PipelineStage=StrategyWorkflowStage.OrderComposition,ExpiresAtUtc=c?.ExpiresAtUtc??default,
            ReasonCode=reason,InputPayloadSha256=c?.InputSha256??string.Empty
        };
    }
}
