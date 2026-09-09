using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;

/// <summary>Verifies Scylla evidence, then appends one workflow event containing the complete saved dispatch.</summary>
public static class AcceptOrderCompositionPreparation
{
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this AcceptOrderCompositionPreparationCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context, IntrinsicTimeStrategyWorkflowCommandState state)
    {
        using var timing_composer_accept_current_view = WorkflowTrace.Start("composer.accept.current_view", null);
        var current = state.CurrentView;
        timing_composer_accept_current_view?.Stop();
        if (current is not { Status: WorkflowStrategyMachineStatus.Started, CurrentStage: StrategyWorkflowStage.OrderComposition }
            || current.WorkflowId != command.WorkflowId || current.WorkflowRevision != command.InputWorkflowRevision
            || current.CompositionDispatch is not null) return new ServiceOk<GuidResult>(new(command.CommandId));
        using var acceptanceTrace = WorkflowTrace.Start("composer.acceptance", current);
        using var timing_composer_accept_preparation_read = WorkflowTrace.Start("composer.accept.preparation_read", current);
        var prepared = await new CompositionPreparationStore(context.DbFactory.MarketDataDb)
            .ReadAsync(CompositionPreparationAcceptance.Key(current), default).ConfigureAwait(false)
            ?? throw new InvalidDataException("Market preparation has not been durably saved.");
        timing_composer_accept_preparation_read?.Stop();
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        if (now >= current.ExpiresAtUtc) throw new InvalidDataException("Workflow expired before composition acceptance.");
        using var timing_composer_accept_validate_and_build = WorkflowTrace.Start("composer.accept.validate_and_build", current);
        var dispatch = CompositionPreparationAcceptance.Accept(current, prepared, command.Evidence, command.CommandId, now);
        timing_composer_accept_validate_and_build?.Stop();
        var next = current with
        {
            WorkflowRevision = dispatch.InputWorkflowRevision, CompositionDispatch = dispatch,
            UpdatedAtUtc = now, CausationId = command.CommandId,
            OrderComposition = current.OrderComposition with { InputWorkflowRevision = dispatch.InputWorkflowRevision }
        };
        using var timing_composer_accept_create_execution = WorkflowTrace.Start("composer.accept.create_execution", current);
        next = next with { CompositionExecution = CompositionDispatch.Create(next, prepared, now) };
        timing_composer_accept_create_execution?.Stop();
        using var timing_composer_accept_state_update = WorkflowTrace.Start("composer.accept.state_update", current);
        state.Update(new WorkflowStrategyStateUpdatedEvent
        {
            Subject = new(ActorType.Event, WorkflowStrategyStateUpdatedEvent.Actor, WorkflowStrategyStateUpdatedEvent.Verb, command.EntityId.Format()),
            Id = Guid.CreateVersion7(new DateTimeOffset(now)), EntityId = command.EntityId, CommandId = command.CommandId,
            AggregateId = command.EntityId.Format(), EventSource = command.EventSource, ReceivedOn = now,
            WorkflowId = next.WorkflowId, WorkflowRevision = next.WorkflowRevision, CorrelationId = next.CorrelationId,
            CausationId = next.CausationId, PreviousStatus = current.Status, State = next, UpdatedAtUtc = now
        }, command);
        timing_composer_accept_state_update?.Stop();
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }
}
