using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
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
/// <summary>Validates the committed market evidence supplied by the producer and records the complete dispatch.</summary>
public static class AcceptOrderCompositionPreparation
{

    /// <param name="command">
    /// The concrete workflow command.
    /// The concrete command intent.
    /// The concrete workflow intent.
    /// The originating workflow command.
    /// </param>
    extension(AcceptOrderCompositionPreparationCommand command)
    {
        /// <summary>Allows retry of an audited request whose workflow transition never committed.</summary>
        public ValueTask<bool> ResumeAfterAuditAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return ValueTask.FromResult(true);
        }

        /// <summary>Prepares the workflow decision, validates its immutable changes, and applies the source events.</summary>
        ///
        /// <param name = "context">The authorized workflow services, clock, and logger.</param>
        /// <param name = "state">The authoritative workflow state.</param>
        /// <returns>The command acceptance or rejection.</returns>
        public ValueTask<ServiceResult<GuidResult>> ExecuteAsync(ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context, IntrinsicTimeStrategyWorkflowCommandState state)
        {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(state);
            var preparation = new WorkflowSnapshotPreparation(state.WorkflowDefinition);
            var preparationResult = PrepareWorkflow(command, context, preparation);
            if (!preparationResult.Success)
                return ValueTask.FromResult(preparationResult);
            var errorMsg = "IntrinsicTimeStrategyWorkflow.STATE.APPLY_FAILED: unable to apply AcceptOrderCompositionPreparation event";
            var updated = command.Compute(preparation.Freeze(), out var workflowTransition) switch
            {
                _ when workflowTransition.RejectionReason is not null => command.UpdateFailed(ref errorMsg, workflowTransition.RejectionReason),
                _ => state.Update(command.CreateWorkflowLifecycleEvents(workflowTransition), command)
            };
            return ValueTask.FromResult<ServiceResult<GuidResult>>(updated ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId)) : command.UpdateFailed(errorMsg));
        }

        /// <summary>Prepares immutable workflow changes while preserving deadline, stale-result, and financial-read ordering.</summary>
        ///
        /// <param name = "context">The authorized actor services and logging context.</param>
        /// <param name = "state">The authoritative state or local preparation snapshot, as declared by the method.</param>
        /// <returns>The prepared business result or command acceptance.</returns>
        internal ServiceResult<GuidResult> PrepareWorkflow(ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context, WorkflowSnapshotPreparation state)
        {
            using var timing_composer_accept_current_view = WorkflowTrace.Start("composer.accept.current_view", null);
            var current = state.CurrentView;
            timing_composer_accept_current_view?.Stop();
            if (current is not { Status: WorkflowStrategyMachineStatus.Started, CurrentStage: StrategyWorkflowStage.OrderComposition } || current.WorkflowId != command.WorkflowId || current.WorkflowRevision != command.InputWorkflowRevision || current.CompositionDispatch is not null)
                return new ServiceOk<GuidResult>(new(command.CommandId));
            using var acceptanceTrace = WorkflowTrace.Start("composer.acceptance", current);
            if (command.PreparedMarketPayload is not { Length: > 0 and < TradeSelectionContracts.MaximumTransportBytes } payload)
                throw new InvalidDataException("Committed market preparation payload is unavailable or too large.");
            var prepared = MessagePackBinarySerializer.Shared.Deserialize<CompositionPreparation>(payload)
                ?? throw new InvalidDataException("Committed market preparation payload is invalid.");
            var now = context.TimeProvider.GetUtcNow().UtcDateTime;
            if (now >= current.ExpiresAtUtc)
                throw new InvalidDataException("Workflow expired before composition acceptance.");
            using var timing_composer_accept_validate_and_build = WorkflowTrace.Start("composer.accept.validate_and_build", current);
            var dispatch = CompositionPreparationAcceptance.Accept(current, prepared, command.Evidence, command.CommandId, now);
            timing_composer_accept_validate_and_build?.Stop();
            var next = current with
            {
                WorkflowRevision = dispatch.InputWorkflowRevision,
                CompositionDispatch = dispatch,
                UpdatedAtUtc = now,
                CausationId = command.CommandId,
                OrderComposition = current.OrderComposition with
                {
                    InputWorkflowRevision = dispatch.InputWorkflowRevision
                }
            };
            using var timing_composer_accept_create_execution = WorkflowTrace.Start("composer.accept.create_execution", current);
            next = next with
            {
                CompositionExecution = CompositionDispatch.Create(next, prepared, now)
            };
            timing_composer_accept_create_execution?.Stop();
            using var timing_composer_accept_state_update = WorkflowTrace.Start("composer.accept.state_update", current);
            state.Record(new WorkflowSnapshotChange { SnapshotEventId = Guid.CreateVersion7(new DateTimeOffset(now)), EntityId = command.EntityId, WorkflowId = next.WorkflowId, WorkflowRevision = next.WorkflowRevision, CorrelationId = next.CorrelationId, CausationId = next.CausationId, PreviousStatus = current.Status, WorkflowDefinition = next, UpdatedAtUtc = now }, command);
            timing_composer_accept_state_update?.Stop();
            return new ServiceOk<GuidResult>(new(command.CommandId));
        }

        /// <summary>Checks ownership and required data of prepared workflow changes without mutation.</summary>
        ///
        /// <param name = "workflowChanges">The immutable proposals prepared from the current workflow and observed inputs.</param>
        /// <param name = "workflowTransition">The accepted proposals or business rejection.</param>
        /// <returns>True when every proposed workflow snapshot belongs to the command.</returns>
        internal bool Compute(IReadOnlyList<WorkflowSnapshotChange> workflowChanges, out WorkflowSnapshotTransition workflowTransition)
        {
            workflowTransition = workflowChanges.Any(change => change.WorkflowDefinition is null || !Equals(change.EntityId, command.EntityId)) ? new([], "IntrinsicTimeStrategyWorkflow: the computed workflow snapshot is missing or belongs to another entity.") : new(workflowChanges);
            return workflowTransition.RejectionReason is null;
        }

        /// <summary>Creates each ordered source event with the originating command identity.</summary>
        ///
        /// <param name = "workflowTransition">The accepted workflow snapshots.</param>
        /// <returns>The source events ready for state application.</returns>
        internal IReadOnlyList<WorkflowStrategyStateUpdatedEvent> CreateWorkflowLifecycleEvents(WorkflowSnapshotTransition workflowTransition) => workflowTransition.WorkflowChanges.Select(change => new WorkflowStrategyStateUpdatedEvent { CommandId = command.CommandId, Subject = new(ActorType.Event, WorkflowStrategyStateUpdatedEvent.Actor, WorkflowStrategyStateUpdatedEvent.Verb, command.EntityId.Format()), Id = change.SnapshotEventId, EntityId = command.EntityId, AggregateId = command.EntityId.Format(), EventSource = command.EventSource, ReceivedOn = change.UpdatedAtUtc, WorkflowId = change.WorkflowId, WorkflowRevision = change.WorkflowRevision, CorrelationId = change.CorrelationId, CausationId = change.CausationId, PreviousStatus = change.PreviousStatus, WorkflowDefinition = change.WorkflowDefinition, UpdatedAtUtc = change.UpdatedAtUtc }).ToArray();
    }
}
