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
        using var trace = WorkflowTrace.Start("risk.dispatch", snapshot.State);
        var read=new RedispatchCurrentStrategyPipelineCommand
        {
            EntityId=snapshot.EntityId,Subject=new(ActorType.Command,RedispatchCurrentStrategyPipelineCommand.Actor,
                RedispatchCurrentStrategyPipelineCommand.Verb,snapshot.EntityId.Format())
        };
        var loaded = await context.WorkflowRepository.LoadStateAsync(read).ConfigureAwait(false);
        using var currentViewTrace = WorkflowTrace.Start("risk.dispatch.current_view", snapshot.State);
        var view = loaded.CurrentView;
        currentViewTrace?.Stop();
        if (view is not { Status:WorkflowStrategyMachineStatus.Started,CurrentStage:StrategyWorkflowStage.RiskManagement }
            || view.WorkflowId!=snapshot.WorkflowId || view.WorkflowRevision!=snapshot.WorkflowRevision) return;
        if (view.RiskManagement.ProcessingStatus==StrategyActorProcessingStatus.Completed)
        {
            await view.ExecuteFinancialHandoffAsync(context).ConfigureAwait(false);
            return;
        }
        if (view.RiskExecution is not { } execute)
        {
            await EnsureFundCompositionAsync(view, context).ConfigureAwait(false);
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

    // Only a workflow-accepted Composer result may advance the Fund. Read each committed
    // checkpoint first so redispatch after a lost reply never repeats a versioned mutation.
    internal static async Task EnsureFundCompositionAsync(IntrinsicTimeStrategyWorkflowView view,
        IIntrinsicTimeStrategyWorkflowRealtimeContext context)
    {
        using var trace = WorkflowTrace.Start("risk.fund_composition", view);
        var result = view.OrderComposition.Result!.ReadCompositionResult();
        var candidate = result.Candidate!;
        using var readTrace = WorkflowTrace.Start("risk.fund_composition.get_order", view);
        var read = await context.PortfolioQueries.GetOrderAsync(checked((int)candidate.OrderId)).ConfigureAwait(false);
        readTrace?.Stop();
        var order = read.Value;
        RiskUnitModel.Require(read.Success && order is not null && order.WorkflowId == view.WorkflowId.Value
            && order.PortfolioId == candidate.PortfolioId && order.FundId == candidate.FundId,
            "RM.HANDOFF.FUND_NOT_READY");
        var id = new Domain.Portfolio.Shared.Identities.PortfolioFundOrderId(candidate.PortfolioId, candidate.FundId, checked((int)candidate.OrderId));
        if (order!.Status == "TemplateSelected")
        {
            using var composingTrace = WorkflowTrace.Start("risk.fund_composition.mark_composing", view);
            read = await context.PortfolioCommands.MarkComposingAsync(id, order.AggregateVersion,
                view.CompositionExecution!.CommandId).ConfigureAwait(false);
            RiskUnitModel.Require(read.Success && read.Value is not null, "RM.HANDOFF.FUND_NOT_READY");
            order = read.Value!;
        }
        if (order.Status == "Composing")
        {
            using var composedTrace = WorkflowTrace.Start("risk.fund_composition.record_composed", view);
            read = await context.PortfolioCommands.RecordComposedAsync(id, order.AggregateVersion, new()
            {
                ResultId = result.ResultId, ResultSha256 = view.OrderComposition.Result.PayloadSha256,
                InvocationId = view.CompositionExecution!.CommandId,
                EvaluatedAtUtc = view.CompositionExecution.EvaluatedAtUtc, ExpiresAtUtc = candidate.ValidUntilUtc
            }).ConfigureAwait(false);
            RiskUnitModel.Require(read.Success && read.Value is not null, "RM.HANDOFF.FUND_NOT_READY");
            order = read.Value!;
        }
        RiskUnitModel.Require(order.Status == "RiskPending" && order.CompositionResultId == result.ResultId
            && order.CompositionResultHash == view.OrderComposition.Result.PayloadSha256, "RM.HANDOFF.FUND_NOT_READY");
    }
}
