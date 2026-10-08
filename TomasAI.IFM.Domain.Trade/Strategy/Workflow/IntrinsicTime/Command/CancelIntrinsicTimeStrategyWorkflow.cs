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

using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Model;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;

/// <summary>Handles explicit workflow cancellation.</summary>
public static class CancelIntrinsicTimeStrategyWorkflow
{
    /// <summary>Marks the current workflow stage and workflow as cancelled.</summary>
    /// <param name="command">The concrete workflow command.</param>
    /// <param name="context">The authorized workflow services, clock, and logger.</param>
    /// <param name="state">The authoritative workflow state.</param>
    /// <returns>The command acceptance or rejection.</returns>
    public static ServiceResult<GuidResult> Execute(this CancelIntrinsicTimeStrategyWorkflowCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IntrinsicTimeStrategyWorkflowCommandState state)
    {
        var preparation = new WorkflowSnapshotPreparation(state.WorkflowDefinition);
        var preparationResult = PrepareWorkflow(command, context, preparation);
        if (!preparationResult.Success) return preparationResult;
        var errorMsg = "IntrinsicTimeStrategyWorkflow.STATE.APPLY_FAILED: unable to apply CancelIntrinsicTimeStrategyWorkflow event";
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
    internal static ServiceResult<GuidResult> PrepareWorkflow(this CancelIntrinsicTimeStrategyWorkflowCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        WorkflowSnapshotPreparation state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);

        var current = state.CurrentView;
        if (current is not { Status: WorkflowStrategyMachineStatus.Started } ||
            current.WorkflowId != command.WorkflowId ||
            current.WorkflowRevision != command.ExpectedWorkflowRevision)
        {
            context.Logger.LogWarning(
                "{Component}.{Method} "+"Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",nameof(CancelIntrinsicTimeStrategyWorkflow),nameof(Execute),                command.CommandName,command.Subject.EntityId,current?.WorkflowId,current?.WorkflowRevision);
            return Ok(command);
        }

        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        var failure = new StrategyPipelineFailure
        {
            ErrorMessage = command.ReasonCode,
            ErrorType = "Cancelled",
            FailedAtUtc = now
        };
        var cancelledStage = CurrentStage(current) with
        {
            ProcessingStatus = StrategyActorProcessingStatus.Cancelled,
            FailedAtUtc = now,
            Failure = failure
        };
        var cancelled = SetCurrentStage(current with
        {
            Status = WorkflowStrategyMachineStatus.Cancelled,
            WorkflowRevision = current.WorkflowRevision + 1,
            CausationId = command.CommandId,
            UpdatedAtUtc = now,
            TerminalAtUtc = now,
            StopReasonCode = command.ReasonCode
        }, cancelledStage);
        state.Record(new WorkflowSnapshotChange
        {
            SnapshotEventId = Guid.CreateVersion7(new DateTimeOffset(now, TimeSpan.Zero)),
            EntityId = command.EntityId,
            WorkflowId = cancelled.WorkflowId,
            WorkflowRevision = cancelled.WorkflowRevision,
            CorrelationId = cancelled.CorrelationId,
            CausationId = cancelled.CausationId,
            PreviousStatus = current.Status,
            WorkflowDefinition = cancelled,
            UpdatedAtUtc = now
        }, command);
        return Ok(command);
    }

    /// <summary>Evaluates current stage business information.</summary>
    /// <param name="view">The immutable workflow snapshot.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    static StrategyWorkflowStageState CurrentStage(IntrinsicTimeStrategyWorkflowView view)
        => view.CurrentStage switch
        {
            StrategyWorkflowStage.RegimeDiscovery => view.RegimeDiscovery,
            StrategyWorkflowStage.MarketCondition => view.MarketCondition,
            StrategyWorkflowStage.TradeSelection => view.TradeSelection,
            StrategyWorkflowStage.OrderComposition => view.OrderComposition,
            StrategyWorkflowStage.RiskManagement => view.RiskManagement,
            _ => throw new ArgumentOutOfRangeException(nameof(view), view.CurrentStage, "A concrete stage is required.")
        };

    /// <summary>Evaluates set current stage business information.</summary>
    /// <param name="view">The immutable workflow snapshot.</param>
    /// <param name="stage">The stage business information.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    static IntrinsicTimeStrategyWorkflowView SetCurrentStage(
        IntrinsicTimeStrategyWorkflowView view,
        StrategyWorkflowStageState stage)
        => view.CurrentStage switch
        {
            StrategyWorkflowStage.RegimeDiscovery => view with { RegimeDiscovery = stage },
            StrategyWorkflowStage.MarketCondition => view with { MarketCondition = stage },
            StrategyWorkflowStage.TradeSelection => view with { TradeSelection = stage },
            StrategyWorkflowStage.OrderComposition => view with { OrderComposition = stage },
            StrategyWorkflowStage.RiskManagement => view with { RiskManagement = stage },
            _ => throw new ArgumentOutOfRangeException(nameof(view), view.CurrentStage, "A concrete stage is required.")
        };

    /// <summary>Evaluates ok business information.</summary>
    /// <param name="command">The concrete command intent.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    static ServiceResult<GuidResult> Ok(CancelIntrinsicTimeStrategyWorkflowCommand command)
        => new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
    /// <summary>Checks ownership and required data of prepared workflow changes without mutation.</summary>
    /// <param name="command">The concrete workflow intent.</param>
    /// <param name="workflowChanges">The immutable proposals prepared from the current workflow and observed inputs.</param>
    /// <param name="workflowTransition">The accepted proposals or business rejection.</param>
    /// <returns>True when every proposed workflow snapshot belongs to the command.</returns>
    internal static bool Compute(this CancelIntrinsicTimeStrategyWorkflowCommand command, IReadOnlyList<WorkflowSnapshotChange> workflowChanges, out WorkflowSnapshotTransition workflowTransition)
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
    internal static IReadOnlyList<WorkflowStrategyStateUpdatedEvent> CreateWorkflowLifecycleEvents(this CancelIntrinsicTimeStrategyWorkflowCommand command, WorkflowSnapshotTransition workflowTransition)
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
