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
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;

public static class CompleteTradeSelectionReservation
{
    public static ServiceResult<GuidResult> Execute(this CompleteTradeSelectionReservationCommand c, ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context, IntrinsicTimeStrategyWorkflowCommandState state)
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
        state.Update(new WorkflowStrategyStateUpdatedEvent
        {
            Subject = new(ActorType.Event, WorkflowStrategyStateUpdatedEvent.Actor, WorkflowStrategyStateUpdatedEvent.Verb, c.EntityId.Format()),
            Id = Guid.CreateVersion7(new DateTimeOffset(now)),
            EntityId = c.EntityId,
            CommandId = c.CommandId,
            AggregateId = c.EntityId.Format(),
            EventSource = c.EventSource,
            ReceivedOn = now,
            WorkflowId = updated.WorkflowId,
            WorkflowRevision = updated.WorkflowRevision,
            CorrelationId = updated.CorrelationId,
            CausationId = updated.CausationId,
            PreviousStatus = current.Status,
            State = updated,
            UpdatedAtUtc = now
        }, c);
        return new ServiceOk<GuidResult>(new(c.CommandId));
    }
}
