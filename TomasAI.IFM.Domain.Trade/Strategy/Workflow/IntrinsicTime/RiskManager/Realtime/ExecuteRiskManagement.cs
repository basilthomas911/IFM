using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Realtime.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime;

/// <summary>Maps committed workflow snapshots to preparation or the exact saved Function request. It never constructs fresh retry inputs.</summary>
public static class ExecuteRiskManagement
{
    public static async ValueTask ExecuteAsync(this WorkflowStrategyStateUpdatedEvent snapshot,IIntrinsicTimeStrategyWorkflowRealtimeContext context)
    {
        var read=new RedispatchCurrentStrategyPipelineCommand
        {
            EntityId=snapshot.EntityId,Subject=new(ActorType.Command,RedispatchCurrentStrategyPipelineCommand.Actor,
                RedispatchCurrentStrategyPipelineCommand.Verb,snapshot.EntityId.Format())
        };
        var view=(await context.WorkflowRepository.LoadStateAsync(read).ConfigureAwait(false)).CurrentView;
        if (view is not { Status:WorkflowStrategyMachineStatus.Started,CurrentStage:StrategyWorkflowStage.RiskManagement }
            || view.WorkflowId!=snapshot.WorkflowId || view.WorkflowRevision!=snapshot.WorkflowRevision) return;
        if (view.RiskManagement.ProcessingStatus==StrategyActorProcessingStatus.Completed)
        {
            await view.ExecuteFinancialHandoffAsync(context).ConfigureAwait(false);
            return;
        }
        if (view.RiskExecution is not { } execute)
        {
            var prepare=new PrepareRiskManagementCommand
            {
                CommandId=StableId(view.WorkflowId,view.WorkflowRevision,view.OrderComposition.SourceEventId,PrepareRiskManagementCommand.Verb),
                EntityId=view.EntityId,Subject=new(ActorType.Command,PrepareRiskManagementCommand.Actor,PrepareRiskManagementCommand.Verb,view.EntityId.Format()),
                WorkflowId=view.WorkflowId,InputWorkflowRevision=view.WorkflowRevision
            };
            await context.SendAsync<PrepareRiskManagementCommand,IntrinsicTimeStrategyWorkflowEntityId>(prepare,view.EntityId).ConfigureAwait(false);
            return;
        }
        RiskUnitModel.Require(execute.InputSha256==execute.Fingerprint() && execute.WorkflowId==view.WorkflowId
            && execute.InputWorkflowRevision==view.WorkflowRevision,"RM.DISPATCH.INVALID");
        var remaining=execute.ExpiresAtUtc-context.TimeProvider.GetUtcNow().UtcDateTime;
        using var timeout=new CancellationTokenSource((remaining>TimeSpan.Zero ? remaining : TimeSpan.Zero)+TimeSpan.FromSeconds(5));
        // A lost reply is recoverable by redispatching this persisted invocation, not by inventing a failed business outcome.
        var reply=await context.RequestFunctionAsync<ExecuteRiskManagementPipelineCommand,RiskManagementExecutionId,
            FunctionResult<RiskManagementFunctionCompletedEvent,RiskManagementFunctionFailedEvent>>(execute,timeout.Token).ConfigureAwait(false);
        var terminal=reply.Value ?? throw new InvalidDataException("Missing Risk Function reply.");
        RiskUnitModel.Require(terminal.IsTerminal,"RM.RESULT.INVALID");
        if (terminal.Completed is { } completed)
        {
            RiskUnitModel.Require(completed.Id==execute.CommandId && completed.CommandId==execute.CommandId
                && completed.WorkflowId==execute.WorkflowId && completed.EntityId==execute.WorkflowEntityId
                && completed.InputWorkflowRevision==execute.InputWorkflowRevision && completed.RequestFingerprint==execute.InputSha256,
                "RM.RESULT.INVALID");
            var command=new CompleteRiskManagementCommand
            {
                CommandId=StableId(execute.WorkflowId,execute.InputWorkflowRevision,execute.CommandId,CompleteRiskManagementCommand.Verb),
                Subject=new(ActorType.Command,CompleteRiskManagementCommand.Actor,CompleteRiskManagementCommand.Verb,execute.WorkflowEntityId.Format()),
                EntityId=execute.WorkflowEntityId,WorkflowId=execute.WorkflowId,InputWorkflowRevision=execute.InputWorkflowRevision,
                SourceEventId=completed.Id,Result=StrategyStageResultEnvelope.CreateRisk(completed.Result),
                CorrelationId=execute.CorrelationId,CausationId=completed.Id,CompletedAtUtc=completed.Result.ProducedAtUtc
            };
            await context.SendAsync<CompleteRiskManagementCommand,IntrinsicTimeStrategyWorkflowEntityId>(command,command.EntityId).ConfigureAwait(false);
        }
        else
        {
            var failed=terminal.Failed!;
            RiskUnitModel.Require(failed.CommandId==execute.CommandId && failed.WorkflowId==execute.WorkflowId
                && failed.EntityId==execute.WorkflowEntityId && failed.InputWorkflowRevision==execute.InputWorkflowRevision,"RM.RESULT.INVALID");
            var command=new FailRiskManagementCommand
            {
                CommandId=StableId(execute.WorkflowId,execute.InputWorkflowRevision,execute.CommandId,FailRiskManagementCommand.Verb),
                Subject=new(ActorType.Command,FailRiskManagementCommand.Actor,FailRiskManagementCommand.Verb,execute.WorkflowEntityId.Format()),
                EntityId=execute.WorkflowEntityId,WorkflowId=execute.WorkflowId,InputWorkflowRevision=execute.InputWorkflowRevision,
                SourceEventId=execute.CommandId,CorrelationId=execute.CorrelationId,CausationId=execute.CommandId,FailedAtUtc=failed.ErrorDate,
                Failure=new() { ErrorCode=failed.ErrorCode,ErrorType="RiskManagementFailed",ErrorMessage=failed.ErrorMessage,
                    ErrorData=failed.ReasonCode,FailedAtUtc=failed.ErrorDate }
            };
            await context.SendAsync<FailRiskManagementCommand,IntrinsicTimeStrategyWorkflowEntityId>(command,command.EntityId).ConfigureAwait(false);
        }
    }
    static Guid StableId(StrategyWorkflowId workflow,long revision,Guid invocation,string verb)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes($"{workflow}|{revision}|{invocation}|{verb}")).AsSpan(0,16));
}
