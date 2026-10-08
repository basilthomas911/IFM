using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Model;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;

public static class CompleteTradeSelectionReservation
{
    /// <summary>Prepares the workflow decision, validates its immutable changes, and applies the source events.</summary>
    /// <param name="c">The concrete workflow command.</param>
    /// <param name="context">The authorized workflow services, clock, and logger.</param>
    /// <param name="state">The authoritative workflow state.</param>
    /// <returns>The command acceptance or rejection.</returns>
    public static ServiceResult<GuidResult> Execute(this CompleteTradeSelectionReservationCommand c, ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context, IntrinsicTimeStrategyWorkflowCommandState state)
    {
        var preparation = new WorkflowSnapshotPreparation(state.WorkflowDefinition);
        var preparationResult = PrepareWorkflow(c, context, preparation);
        if (!preparationResult.Success) return preparationResult;
        var errorMsg = "IntrinsicTimeStrategyWorkflow.STATE.APPLY_FAILED: unable to apply CompleteTradeSelectionReservation event";
        var updated = c.Compute(preparation.Freeze(), out var workflowTransition) switch
        {
            _ when workflowTransition.RejectionReason is not null => c.UpdateFailed(ref errorMsg, workflowTransition.RejectionReason),
            _ => state.Update(c.CreateWorkflowLifecycleEvents(workflowTransition), c)
        };
        return updated ? new ServiceOk<GuidResult>(new GuidResult(c.CommandId)) : c.UpdateFailed(errorMsg);
    }
    /// <summary>Prepares immutable workflow changes while preserving deadline, stale-result, and financial-read ordering.</summary>
    /// <param name="c">The concrete command intent.</param>
    /// <param name="context">The authorized actor services and logging context.</param>
    /// <param name="state">The authoritative state or local preparation snapshot, as declared by the method.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    internal static ServiceResult<GuidResult> PrepareWorkflow(this CompleteTradeSelectionReservationCommand c, ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context, WorkflowSnapshotPreparation state)
    {
        var current = state.CurrentView;
        if (current is null || current.WorkflowId != c.WorkflowId || current.CompositionHandoff is not { Status: CompositionHandoffStatus.ReservationPending } pending
            || pending.SelectionSourceEventId != c.SourceEventId || pending.AcceptedSelectionRevision != c.InputWorkflowRevision || pending.ReservationRequestSha256 != c.ReservationRequestSha256)
            return new ServiceOk<GuidResult>(new(c.CommandId));
        TradeSelectionHandoff.ValidateReservation(pending, c.Reservation);
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        var stillCurrent = current.Status == WorkflowStrategyMachineStatus.Started && current.CurrentStage == StrategyWorkflowStage.TradeSelection && current.WorkflowRevision == c.InputWorkflowRevision
            && now < current.ExpiresAtUtc && now < pending.Request.ExpiresAtUtc && c.Reservation.Order.Status == "TemplateSelected";
        var updated = current with
        {
            WorkflowRevision = current.WorkflowRevision + 1,
            UpdatedAtUtc = now,
            CausationId = c.CommandId,
            CompositionHandoff = pending with { Status = stillCurrent ? CompositionHandoffStatus.Reserved : CompositionHandoffStatus.Stopped, Reservation = c.Reservation, UpdatedAtUtc = now },
            CurrentStage = stillCurrent ? StrategyWorkflowStage.OrderComposition : current.CurrentStage,
            Status = stillCurrent ? current.Status : current.Status == WorkflowStrategyMachineStatus.Started ? WorkflowStrategyMachineStatus.TimedOut : current.Status,
            Outcome = stillCurrent ? current.Outcome : current.Status == WorkflowStrategyMachineStatus.Started ? StrategyWorkflowOutcome.TimedOut : current.Outcome,
            TerminalAtUtc = stillCurrent ? current.TerminalAtUtc : current.TerminalAtUtc ?? now,
            StopReasonCode = stillCurrent ? current.StopReasonCode : "TS.RESERVATION.STOPPED",
            OrderComposition = stillCurrent ? new() { ProcessingStatus = StrategyActorProcessingStatus.Processing, StartedAtUtc = now, InputWorkflowRevision = current.WorkflowRevision + 1, ExpiresAtUtc = pending.Request.ExpiresAtUtc } : current.OrderComposition
        };
        state.Record(new WorkflowSnapshotChange
        {
            SnapshotEventId = Guid.CreateVersion7(new DateTimeOffset(now)),
            EntityId = c.EntityId,
            WorkflowId = updated.WorkflowId,
            WorkflowRevision = updated.WorkflowRevision,
            CorrelationId = updated.CorrelationId,
            CausationId = updated.CausationId,
            PreviousStatus = current.Status,
            WorkflowDefinition = updated,
            UpdatedAtUtc = now
        }, c);
        return new ServiceOk<GuidResult>(new(c.CommandId));
    }
    /// <summary>Checks ownership and required data of prepared workflow changes without mutation.</summary>
    /// <param name="command">The concrete workflow intent.</param>
    /// <param name="workflowChanges">The immutable proposals prepared from the current workflow and observed inputs.</param>
    /// <param name="workflowTransition">The accepted proposals or business rejection.</param>
    /// <returns>True when every proposed workflow snapshot belongs to the command.</returns>
    internal static bool Compute(this CompleteTradeSelectionReservationCommand command, IReadOnlyList<WorkflowSnapshotChange> workflowChanges, out WorkflowSnapshotTransition workflowTransition)
    {
        workflowTransition = workflowChanges.Any(change => change.WorkflowDefinition is null || !Equals(change.EntityId, command.EntityId))
            ? new([], "IntrinsicTimeStrategyWorkflow: the computed workflow snapshot is missing or belongs to another entity.")
            : new(workflowChanges);
        return workflowTransition.RejectionReason is null;
    }
    /// <summary>Creates each ordered source event with the originating command identity.</summary>
    /// <param name="command">The originating workflow command.</param>
    /// <param name="workflowTransition">The accepted workflow snapshots.</param>
    /// <returns>The source events ready for state application.</returns>
    internal static IReadOnlyList<WorkflowStrategyStateUpdatedEvent> CreateWorkflowLifecycleEvents(this CompleteTradeSelectionReservationCommand command, WorkflowSnapshotTransition workflowTransition)
        => workflowTransition.WorkflowChanges.Select(change => new WorkflowStrategyStateUpdatedEvent
        {
            CommandId = command.CommandId,
            Subject = new(ActorType.Event, WorkflowStrategyStateUpdatedEvent.Actor, WorkflowStrategyStateUpdatedEvent.Verb, command.EntityId.Format()),
            Id = change.SnapshotEventId,
            EntityId = command.EntityId,
            AggregateId = command.EntityId.Format(),
            EventSource = command.EventSource,
            ReceivedOn = change.UpdatedAtUtc,
            WorkflowId = change.WorkflowId,
            WorkflowRevision = change.WorkflowRevision,
            CorrelationId = change.CorrelationId,
            CausationId = change.CausationId,
            PreviousStatus = change.PreviousStatus,
            WorkflowDefinition = change.WorkflowDefinition,
            UpdatedAtUtc = change.UpdatedAtUtc
        }).ToArray();
}
