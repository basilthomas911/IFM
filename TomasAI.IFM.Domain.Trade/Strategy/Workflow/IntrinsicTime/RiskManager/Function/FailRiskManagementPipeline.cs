using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function;

/// <summary>Maps contract, calculation, transport and lifecycle failures to non-durable terminal events.</summary>
public static class FailRiskManagementPipeline
{
    /// <summary>Classifies a failure and builds its event without persisting incomplete state.</summary>
    public static FunctionResult<RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent> Fail(
        this FunctionEventContext<ExecuteRiskManagementPipelineCommand> input, TimeProvider clock)
    {
        if (!input.IsConflict && input.Exception is null) throw new ArgumentException("Failure requires a conflict or exception.");
        var reason = input.IsConflict ? "RM.INPUT.CONFLICTING_DUPLICATE"
            : input.Exception is RiskCalculationException validation ? validation.ReasonCode
            : input.Exception is TimeoutException ? "RM.TIME.EXPIRED" : input.Stage switch
            {
                FunctionFailureStage.Loading or FunctionFailureStage.Persistence => "RM.PERSISTENCE.FAILED",
                FunctionFailureStage.Projection => "RM.PROJECTION.FAILED",
                FunctionFailureStage.Execution => "RM.CALCULATION.FAILED",
                _ => "RM.INPUT.INVALID"
            };
        if (!input.IsConflict && input.Exception is TomasAI.IFM.Shared.Exceptions.CommandValidationException mapped)
        {
            var code = System.Text.RegularExpressions.Regex.Match(mapped.Message, @"RM\.[A-Z_]+\.[A-Z_]+");
            if (code.Success) reason = code.Value;
        }
        return FunctionResult<RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent>.Fail(CreateFailedEvent(input.Request, reason, clock));
    }

    /// <summary>Constructs failure metadata for valid or malformed ingress.</summary>
    static RiskManagementFunctionFailedEvent CreateFailedEvent(ExecuteRiskManagementPipelineCommand? c,string reason,TimeProvider clock)
    {
        var now=clock.GetUtcNow().UtcDateTime;
        return new()
        {
            Subject=new(ActorType.Function,ExecuteRiskManagementPipelineCommand.Actor,RiskManagementFunctionFailedEvent.Verb,c?.EntityId.Format()??string.Empty),
            Id=Guid.NewGuid(),EntityId=c?.WorkflowEntityId??default,WorkflowId=c?.WorkflowId??default,CommandId=c?.CommandId??Guid.Empty,InputWorkflowRevision=c?.InputWorkflowRevision??0,
            ErrorDate=now,ReceivedOn=now,ErrorCode=RiskManagementFunctionFailedEvent.ErrorId,ErrorType=ErrorType.Command,ErrorData=reason,ErrorMessage="Risk assessment failed: "+reason,
            EventSource=c?.EventSource??ExecuteRiskManagementPipelineCommand.Actor,AggregateId=c?.EntityId.Format()??string.Empty,CommandName=nameof(ExecuteRiskManagementPipelineCommand),RouteTo=c?.RouteTo.ToString()??string.Empty,
            CorrelationId=c?.CorrelationId??Guid.Empty,CausationId=c?.CausationId??Guid.Empty,PipelineStage=StrategyWorkflowStage.RiskManagement,ExpiresAtUtc=c?.ExpiresAtUtc??default,
            ReasonCode=reason,InputPayloadSha256=c?.InputSha256??string.Empty
        };
    }
}
