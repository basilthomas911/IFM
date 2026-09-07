using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;

/// <summary>Handles successful Trade Selection completion.</summary>
public static class CompleteTradeSelection
{
    /// <summary>Records the Trade Selection result and selects Order Composition.</summary>
    public static ServiceResult<GuidResult> Execute(this CompleteTradeSelectionCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IntrinsicTimeStrategyWorkflowCommandState state)
    {
        ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        var current = state.CurrentView;
        if (current is not { Status: WorkflowStrategyMachineStatus.Started } ||
            current.WorkflowId != command.WorkflowId || current.WorkflowRevision != command.InputWorkflowRevision ||
            current.CurrentStage != StrategyWorkflowStage.TradeSelection ||
            current.TradeSelection.SourceEventId == command.SourceEventId)
        {
            context.Logger.LogWarning("Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
                command.CommandName, command.Subject.EntityId, current?.WorkflowId, current?.WorkflowRevision);
            return Ok(command);
        }
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        if (now >= current.ExpiresAtUtc)
        {
            var failure = TimeoutFailure(now);
            var timedOut = current with
            {
                Status = WorkflowStrategyMachineStatus.TimedOut, Outcome = StrategyWorkflowOutcome.TimedOut, WorkflowRevision = current.WorkflowRevision + 1,
                CausationId = command.SourceEventId, UpdatedAtUtc = now, TerminalAtUtc = now,
                StopReasonCode = "WorkflowExecutionExpired",
                TradeSelection = current.TradeSelection with
                {
                    ProcessingStatus = StrategyActorProcessingStatus.TimedOut, FailedAtUtc = now,
                    Failure = failure, SourceEventId = command.SourceEventId
                }
            };
            AppendSnapshot(state, command, current.Status, timedOut, now);
            context.Logger.LogWarning("Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
                command.Subject.EntityId, timedOut.WorkflowId, timedOut.WorkflowRevision);
            return Ok(command);
        }
        TradeSelectionResult result;
        try
        {
            result=TradeSelectionContracts.ReadResult(command.Result);
            var dispatch=current.SelectionDispatch??throw new ArgumentException("Missing durable selector dispatch.");
            var expected=TradeSelectionEvaluator.Evaluate(dispatch);
            TradeSelectionContracts.Require(command.SourceEventId==result.ResultId && result.InputWorkflowRevision==command.InputWorkflowRevision
                && TradeSelectionContracts.EvidenceHash(expected)==TradeSelectionContracts.EvidenceHash(result),"TS.RESULT.INVALID","Selector result differs from the deterministic decision over the saved request.");
        }
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException)
        {
            var invalid=current with {Status=WorkflowStrategyMachineStatus.Failed,Outcome=StrategyWorkflowOutcome.PipelineFailed,WorkflowRevision=current.WorkflowRevision+1,UpdatedAtUtc=now,TerminalAtUtc=now,StopReasonCode="TS.RESULT.INVALID",
                TradeSelection=current.TradeSelection with {ProcessingStatus=StrategyActorProcessingStatus.Failed,FailedAtUtc=now,SourceEventId=command.SourceEventId,Failure=new(){ErrorCode=23023,ErrorType="SelectionResultInvalid",ErrorMessage=ex.Message,FailedAtUtc=now}}};
            AppendSnapshot(state,command,current.Status,invalid,now);return Ok(command);
        }
        if (now >= result.ValidUntilUtc)
        {
            var timedOut = current with
            {
                Status = WorkflowStrategyMachineStatus.TimedOut, Outcome = StrategyWorkflowOutcome.TimedOut,
                WorkflowRevision = current.WorkflowRevision + 1, UpdatedAtUtc = now, TerminalAtUtc = now,
                CausationId = command.SourceEventId, StopReasonCode = "TS.TIME.EXPIRED",
                TradeSelection = current.TradeSelection with
                {
                    ProcessingStatus = StrategyActorProcessingStatus.TimedOut, FailedAtUtc = now,
                    Failure = TimeoutFailure(now), SourceEventId = command.SourceEventId, Result = null
                }
            };
            AppendSnapshot(state, command, current.Status, timedOut, now);
            return Ok(command);
        }
        var noTrade=result.Outcome==SelectionOutcome.NoTrade;
        var revision = current.WorkflowRevision + 1;
        var updated = current with
        {
            CausationId = command.CausationId, WorkflowRevision = revision, UpdatedAtUtc = now,
            CurrentStage = StrategyWorkflowStage.TradeSelection,
            Status=noTrade?WorkflowStrategyMachineStatus.Completed:WorkflowStrategyMachineStatus.Started,
            Outcome=noTrade?StrategyWorkflowOutcome.NoTrade:StrategyWorkflowOutcome.None,
            TerminalAtUtc=noTrade?now:null,StopReasonCode=noTrade?result.PrimaryReasonCode:string.Empty,
            TradeSelection = current.TradeSelection with
            {
                ProcessingStatus = StrategyActorProcessingStatus.Completed,
                ContinuationDecision = noTrade?StrategyWorkflowContinuationDecision.Stop:StrategyWorkflowContinuationDecision.Proceed,
                CompletedAtUtc = now, FailedAtUtc = null, Result = command.Result, Failure = null,
                SourceEventId = command.SourceEventId, ContinuationRuleSetId = "ts-rank-v1",
                ContinuationRuleSetVersion = 1, ContinuationReasonCodes = [result.PrimaryReasonCode]
            },
            CompositionHandoff=noTrade?null:TradeSelectionHandoff.Pending(result,command.Result,revision,command.SourceEventId,now)
        };
        AppendSnapshot(state, command, current.Status, updated, now);
        return Ok(command);
    }

    static void AppendSnapshot(IntrinsicTimeStrategyWorkflowCommandState state,
        CompleteTradeSelectionCommand command, WorkflowStrategyMachineStatus previousStatus,
        IntrinsicTimeStrategyWorkflowView view, DateTime now)
        => state.Update(new WorkflowStrategyStateUpdatedEvent
        {
            Subject = new ActorSubject(ActorType.Event, WorkflowStrategyStateUpdatedEvent.Actor,
                WorkflowStrategyStateUpdatedEvent.Verb, command.EntityId.Format()),
            Id = Guid.CreateVersion7(new DateTimeOffset(now, TimeSpan.Zero)), EntityId = command.EntityId,
            CommandId = command.CommandId, AggregateId = command.EntityId.Format(), EventSource = command.EventSource,
            ReceivedOn = now, WorkflowId = view.WorkflowId, WorkflowRevision = view.WorkflowRevision,
            CorrelationId = view.CorrelationId, CausationId = view.CausationId, PreviousStatus = previousStatus,
            State = view, UpdatedAtUtc = now
        }, command);

    static StrategyPipelineFailure TimeoutFailure(DateTime now) => new()
    {
        ErrorCode = 23103, ErrorMessage = "The fixed workflow execution deadline was reached.",
        ErrorType = "TradeSelectionTimedOut", FailedAtUtc = now
    };

    static ServiceResult<GuidResult> Ok(CompleteTradeSelectionCommand command)
        => new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
}
