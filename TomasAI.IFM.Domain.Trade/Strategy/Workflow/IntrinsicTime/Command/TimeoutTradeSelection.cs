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

/// <summary>Handles a Trade Selection timeout.</summary>
public static class TimeoutTradeSelection
{
    /// <summary>Marks Trade Selection and the workflow as timed out.</summary>
    public static ServiceResult<GuidResult> Execute(this TimeoutTradeSelectionCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IntrinsicTimeStrategyWorkflowCommandState state)
    {
        ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        var current = state.CurrentView;
        if (current is not { Status: WorkflowStrategyMachineStatus.Started } ||
            current.WorkflowId != command.WorkflowId || current.WorkflowRevision != command.ExpectedWorkflowRevision ||
            current.CurrentStage != StrategyWorkflowStage.TradeSelection)
        {
            LogStale(context, command, current); return Ok(command);
        }
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        var failure = TimeoutFailure(now);
        var updated = current with
        {
            Status = WorkflowStrategyMachineStatus.TimedOut, WorkflowRevision = current.WorkflowRevision + 1,
            CausationId = command.TimeoutId, UpdatedAtUtc = now, TerminalAtUtc = now,
            StopReasonCode = "PipelineTimedOut",
            TradeSelection = current.TradeSelection with
            {
                ProcessingStatus = StrategyActorProcessingStatus.TimedOut, FailedAtUtc = now,
                Failure = failure, SourceEventId = command.TimeoutId
            }
        };
        AppendSnapshot(state, command, current.Status, updated, now);
        context.Logger.LogWarning("Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
            command.Subject.EntityId, updated.WorkflowId, updated.WorkflowRevision);
        return Ok(command);
    }

    static void AppendSnapshot(IntrinsicTimeStrategyWorkflowCommandState state,
        TimeoutTradeSelectionCommand command, WorkflowStrategyMachineStatus previousStatus,
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
        ErrorType = "RegimeDiscoveryTimedOut", FailedAtUtc = now
    };

    static void LogStale(ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        TimeoutTradeSelectionCommand command, IntrinsicTimeStrategyWorkflowView? current)
        => context.Logger.LogWarning("Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
            command.CommandName, command.Subject.EntityId, current?.WorkflowId, current?.WorkflowRevision);

    static ServiceResult<GuidResult> Ok(TimeoutTradeSelectionCommand command)
        => new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
}
