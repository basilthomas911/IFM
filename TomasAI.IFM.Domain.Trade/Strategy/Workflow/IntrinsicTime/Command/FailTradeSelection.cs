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

/// <summary>Handles failed Trade Selection completion.</summary>
public static class FailTradeSelection
{
    /// <summary>Records the Trade Selection failure or timeout and closes the workflow.</summary>
    public static ServiceResult<GuidResult> Execute(this FailTradeSelectionCommand command,
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
            LogStale(context, command, current); return Ok(command);
        }
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        var timedOut = now >= current.ExpiresAtUtc || IsTimeout(command.Failure);
        var updated = current with
        {
            Status = timedOut ? WorkflowStrategyMachineStatus.TimedOut : WorkflowStrategyMachineStatus.Failed,
            WorkflowRevision = current.WorkflowRevision + 1, CausationId = command.CausationId,
            UpdatedAtUtc = now, TerminalAtUtc = now,
            StopReasonCode = timedOut ? "PipelineTimedOut" : command.Failure.ErrorCode.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            TradeSelection = current.TradeSelection with
            {
                ProcessingStatus = timedOut ? StrategyActorProcessingStatus.TimedOut : StrategyActorProcessingStatus.Failed,
                FailedAtUtc = now, Failure = command.Failure, SourceEventId = command.SourceEventId
            }
        };
        AppendSnapshot(state, command, current.Status, updated, now);
        if (timedOut) LogDeadline(context, command, updated);
        return Ok(command);
    }

    static bool IsTimeout(StrategyPipelineFailure failure) => failure.ErrorCode == 23103 ||
        failure.ErrorType.Contains("Timeout", StringComparison.OrdinalIgnoreCase) ||
        failure.ErrorType.Contains("TimedOut", StringComparison.OrdinalIgnoreCase);

    static void AppendSnapshot(IntrinsicTimeStrategyWorkflowCommandState state,
        FailTradeSelectionCommand command, WorkflowStrategyMachineStatus previousStatus,
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

    static void LogStale(ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        FailTradeSelectionCommand command, IntrinsicTimeStrategyWorkflowView? current)
        => context.Logger.LogWarning("Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
            command.CommandName, command.Subject.EntityId, current?.WorkflowId, current?.WorkflowRevision);

    static void LogDeadline(ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        FailTradeSelectionCommand command, IntrinsicTimeStrategyWorkflowView view)
        => context.Logger.LogWarning("Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
            command.Subject.EntityId, view.WorkflowId, view.WorkflowRevision);

    static ServiceResult<GuidResult> Ok(FailTradeSelectionCommand command)
        => new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
}
