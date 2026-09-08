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
        var current = state.CurrentView;
        if (current is not { Status: WorkflowStrategyMachineStatus.Started, CurrentStage: StrategyWorkflowStage.OrderComposition }
            || current.WorkflowId != command.WorkflowId || current.WorkflowRevision != command.InputWorkflowRevision
            || current.CompositionDispatch is not null) return new ServiceOk<GuidResult>(new(command.CommandId));
        var prepared = await new CompositionPreparationStore(context.DbFactory.MarketDataDb)
            .ReadAsync(CompositionPreparationAcceptance.Key(current), default).ConfigureAwait(false)
            ?? throw new InvalidDataException("Market preparation has not been durably saved.");
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        if (now >= current.ExpiresAtUtc) throw new InvalidDataException("Workflow expired before composition acceptance.");
        var dispatch = CompositionPreparationAcceptance.Accept(current, prepared, command.Evidence, command.CommandId, now);
        var next = current with
        {
            WorkflowRevision = dispatch.InputWorkflowRevision, CompositionDispatch = dispatch,
            UpdatedAtUtc = now, CausationId = command.CommandId,
            OrderComposition = current.OrderComposition with { InputWorkflowRevision = dispatch.InputWorkflowRevision }
        };
        state.Update(new WorkflowStrategyStateUpdatedEvent
        {
            Subject = new(ActorType.Event, WorkflowStrategyStateUpdatedEvent.Actor, WorkflowStrategyStateUpdatedEvent.Verb, command.EntityId.Format()),
            Id = Guid.CreateVersion7(new DateTimeOffset(now)), EntityId = command.EntityId, CommandId = command.CommandId,
            AggregateId = command.EntityId.Format(), EventSource = command.EventSource, ReceivedOn = now,
            WorkflowId = next.WorkflowId, WorkflowRevision = next.WorkflowRevision, CorrelationId = next.CorrelationId,
            CausationId = next.CausationId, PreviousStatus = current.Status, State = next, UpdatedAtUtc = now
        }, command);
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }
}
