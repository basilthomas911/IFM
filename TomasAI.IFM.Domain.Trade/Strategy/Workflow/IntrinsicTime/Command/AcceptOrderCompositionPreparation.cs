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

using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Model;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;

/// <summary>Verifies Scylla evidence, then appends one workflow event containing the complete saved dispatch.</summary>
public static class AcceptOrderCompositionPreparation
{
    /// <summary>Prepares the workflow decision, validates its immutable changes, and applies the source events.</summary>
    /// <param name="command">The concrete workflow command.</param>
    /// <param name="context">The authorized workflow services, clock, and logger.</param>
    /// <param name="state">The authoritative workflow state.</param>
    /// <returns>The command acceptance or rejection.</returns>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this AcceptOrderCompositionPreparationCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context, IntrinsicTimeStrategyWorkflowCommandState state)
    {
        var preparation = new WorkflowSnapshotPreparation(state.WorkflowDefinition);
        var preparationResult = await PrepareWorkflowAsync(command, context, preparation).ConfigureAwait(false);
        if (!preparationResult.Success) return preparationResult;
        var errorMsg = "IntrinsicTimeStrategyWorkflow.STATE.APPLY_FAILED: unable to apply AcceptOrderCompositionPreparation event";
        var updated = command.Compute(preparation.Freeze(), out var workflowTransition) switch
        {
            _ when workflowTransition.RejectionReason is not null => command.UpdateFailed(ref errorMsg, workflowTransition.RejectionReason),
            _ => state.Update(command.CreateWorkflowLifecycleEvents(workflowTransition), command)
        };
        return updated ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId)) : command.UpdateFailed(errorMsg);
    }
    /// <summary>Prepares immutable workflow changes while preserving deadline, stale-result, and financial-read ordering.</summary>
    /// <param name="command">The concrete command intent.</param>
    /// <param name="context">The authorized actor services and logging context.</param>
    /// <param name="state">The authoritative state or local preparation snapshot, as declared by the method.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    internal static async ValueTask<ServiceResult<GuidResult>> PrepareWorkflowAsync(this AcceptOrderCompositionPreparationCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context, WorkflowSnapshotPreparation state)
    {
        using var timing_composer_accept_current_view = WorkflowTrace.Start("composer.accept.current_view", null);
        var current = state.CurrentView;
        timing_composer_accept_current_view?.Stop();
        if (current is not { Status: WorkflowStrategyMachineStatus.Started, CurrentStage: StrategyWorkflowStage.OrderComposition }
            || current.WorkflowId != command.WorkflowId || current.WorkflowRevision != command.InputWorkflowRevision
            || current.CompositionDispatch is not null) return new ServiceOk<GuidResult>(new(command.CommandId));
        using var acceptanceTrace = WorkflowTrace.Start("composer.acceptance", current);
        using var timing_composer_accept_preparation_read = WorkflowTrace.Start("composer.accept.preparation_read", current);
        var prepared = await context.DbFactory.MarketDataDb.DbReader
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
            WorkflowRevision = dispatch.InputWorkflowRevision,
            CompositionDispatch = dispatch,
            UpdatedAtUtc = now,
            CausationId = command.CommandId,
            OrderComposition = current.OrderComposition with { InputWorkflowRevision = dispatch.InputWorkflowRevision }
        };
        using var timing_composer_accept_create_execution = WorkflowTrace.Start("composer.accept.create_execution", current);
        next = next with { CompositionExecution = CompositionDispatch.Create(next, prepared, now) };
        timing_composer_accept_create_execution?.Stop();
        using var timing_composer_accept_state_update = WorkflowTrace.Start("composer.accept.state_update", current);
        state.Record(new WorkflowSnapshotChange
        {
            SnapshotEventId = Guid.CreateVersion7(new DateTimeOffset(now)),
            EntityId = command.EntityId,
            WorkflowId = next.WorkflowId,
            WorkflowRevision = next.WorkflowRevision,
            CorrelationId = next.CorrelationId,
            CausationId = next.CausationId,
            PreviousStatus = current.Status,
            WorkflowDefinition = next,
            UpdatedAtUtc = now
        }, command);
        timing_composer_accept_state_update?.Stop();
        return new ServiceOk<GuidResult>(new(command.CommandId));
    }
    /// <summary>Checks ownership and required data of prepared workflow changes without mutation.</summary>
    /// <param name="command">The concrete workflow intent.</param>
    /// <param name="workflowChanges">The immutable proposals prepared from the current workflow and observed inputs.</param>
    /// <param name="workflowTransition">The accepted proposals or business rejection.</param>
    /// <returns>True when every proposed workflow snapshot belongs to the command.</returns>
    internal static bool Compute(this AcceptOrderCompositionPreparationCommand command, IReadOnlyList<WorkflowSnapshotChange> workflowChanges, out WorkflowSnapshotTransition workflowTransition)
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
    internal static IReadOnlyList<WorkflowStrategyStateUpdatedEvent> CreateWorkflowLifecycleEvents(this AcceptOrderCompositionPreparationCommand command, WorkflowSnapshotTransition workflowTransition)
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
