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

/// <summary>Handles workflow execution admission.</summary>
public static class ExecuteIntrinsicTimeStrategyWorkflow
{
    /// <summary>Starts a free workflow or atomically expires and replaces an overdue workflow.</summary>
    /// <param name="command">The concrete workflow command.</param>
    /// <param name="context">The authorized workflow services, clock, and logger.</param>
    /// <param name="state">The authoritative workflow state.</param>
    /// <returns>The command acceptance or rejection.</returns>
    public static ServiceResult<GuidResult> Execute(
        this ExecuteIntrinsicTimeStrategyWorkflowCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IntrinsicTimeStrategyWorkflowCommandState state)
    {
        var preparation = new WorkflowSnapshotPreparation(state.WorkflowDefinition);
        var preparationResult = PrepareWorkflow(command, context, preparation);
        if (!preparationResult.Success) return preparationResult;
        var errorMsg = "IntrinsicTimeStrategyWorkflow.STATE.APPLY_FAILED: unable to apply ExecuteIntrinsicTimeStrategyWorkflow event";
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
    internal static ServiceResult<GuidResult> PrepareWorkflow(
        this ExecuteIntrinsicTimeStrategyWorkflowCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        WorkflowSnapshotPreparation state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);

        var maximumExecutionDuration = context.ExecutionOptions.MaximumExecutionDuration;
        if (maximumExecutionDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(maximumExecutionDuration));

        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        var current = state.CurrentView;
        if (current?.TriggerEventId == command.TriggerEventId)
            return Ok(command);

        // A disabled host may replay established authority, but must not admit
        // a new ITI-triggered workflow through direct or event-bridged commands.
        if (context is IntrinsicTimeStrategyWorkflowCommandContext { WorkflowStartsEnabled: false })
            return Ok(command);

        if (current is { Status: WorkflowStrategyMachineStatus.Started } && now < current.ExpiresAtUtc)
        {
            context.Logger.LogWarning(
                "{Component}.{Method} "+"Workflow Execute rejected as busy for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",nameof(ExecuteIntrinsicTimeStrategyWorkflow),nameof(Execute),                command.EntityId.Format(),current.WorkflowId,current.WorkflowRevision);
            return Ok(command);
        }

        if (current is { Status: WorkflowStrategyMachineStatus.Started })
        {
            var expired = CreateExpiredView(current, command.CommandId, now);
            AppendSnapshot(state, command, current.Status, expired, now);
            context.Logger.LogWarning(
                "{Component}.{Method} "+"Expired workflow {ExpiredWorkflowId} was lazily closed and replaced by {WorkflowId} for {WorkflowEntityId}",nameof(ExecuteIntrinsicTimeStrategyWorkflow),nameof(Execute),                current.WorkflowId,command.ProposedWorkflowId,command.EntityId.Format());
            current = expired;
        }

        var expiresAtUtc = now.Add(maximumExecutionDuration);
        var started = new IntrinsicTimeStrategyWorkflowView
        {
            EntityId = command.EntityId,
            WorkflowId = command.ProposedWorkflowId,
            TriggerEventId = command.TriggerEventId,
            CorrelationId = command.CorrelationId,
            CausationId = command.CausationId,
            WorkflowDefinitionVersion = command.WorkflowDefinitionVersion,
            Status = WorkflowStrategyMachineStatus.Started,
            CurrentStage = StrategyWorkflowStage.RegimeDiscovery,
            WorkflowRevision = 1,
            StartedAtUtc = now,
            UpdatedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc,
            RegimeDiscovery = new StrategyWorkflowStageState
            {
                ProcessingStatus = StrategyActorProcessingStatus.Processing,
                StartedAtUtc = now,
                InputWorkflowRevision = 1,
                ExpiresAtUtc = expiresAtUtc
            },
            Outcome = StrategyWorkflowOutcome.None,
            TriggerEvent = command.TriggerEvent,
            // Explicitly pinned starts retain their frozen selection authority. The
            // Trade Selection boundary validates it before any Function dispatch.
            SelectionBinding = command.SelectionBinding
        };
        AppendSnapshot(state, command, current?.Status ?? WorkflowStrategyMachineStatus.Empty, started, now);
        return Ok(command);
    }

    /// <summary>Creates the command-owned source event from accepted business information.</summary>
    /// <param name="current">The current business snapshot.</param>
    /// <param name="causationId">The causation id business information.</param>
    /// <param name="now">The observed UTC time used for deadline and lifecycle decisions.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    static IntrinsicTimeStrategyWorkflowView CreateExpiredView(
        IntrinsicTimeStrategyWorkflowView current,
        Guid causationId,
        DateTime now)
    {
        var failure = new StrategyPipelineFailure
        {
            ErrorCode = 23103,
            ErrorMessage = "The fixed workflow execution deadline was reached.",
            ErrorType = "RegimeDiscoveryTimedOut",
            FailedAtUtc = now
        };
        var stage = CurrentStage(current) with
        {
            ProcessingStatus = StrategyActorProcessingStatus.TimedOut,
            FailedAtUtc = now,
            Failure = failure
        };
        var expired = current with
        {
            Status = WorkflowStrategyMachineStatus.TimedOut,
            WorkflowRevision = current.WorkflowRevision + 1,
            CausationId = causationId,
            UpdatedAtUtc = now,
            TerminalAtUtc = now,
            StopReasonCode = "WorkflowExecutionExpired"
        };
        return SetCurrentStage(expired, stage);
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

    /// <summary>Evaluates append snapshot business information.</summary>
    /// <param name="state">The authoritative state or local preparation snapshot, as declared by the method.</param>
    /// <param name="command">The concrete command intent.</param>
    /// <param name="previousStatus">The lifecycle status before this transition.</param>
    /// <param name="view">The immutable workflow snapshot.</param>
    /// <param name="now">The observed UTC time used for deadline and lifecycle decisions.</param>
    static void AppendSnapshot(
        WorkflowSnapshotPreparation state,
        ExecuteIntrinsicTimeStrategyWorkflowCommand command,
        WorkflowStrategyMachineStatus previousStatus,
        IntrinsicTimeStrategyWorkflowView view,
        DateTime now)
        => state.Record(new WorkflowSnapshotChange
        {
            SnapshotEventId = Guid.CreateVersion7(new DateTimeOffset(now, TimeSpan.Zero)),
            EntityId = command.EntityId,
            WorkflowId = view.WorkflowId,
            WorkflowRevision = view.WorkflowRevision,
            CorrelationId = view.CorrelationId,
            CausationId = view.CausationId,
            PreviousStatus = previousStatus,
            WorkflowDefinition = view,
            UpdatedAtUtc = now
        }, command);

    /// <summary>Evaluates ok business information.</summary>
    /// <param name="command">The concrete command intent.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    static ServiceResult<GuidResult> Ok(ExecuteIntrinsicTimeStrategyWorkflowCommand command)
        => new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
    /// <summary>Checks ownership and required data of prepared workflow changes without mutation.</summary>
    /// <param name="command">The concrete workflow intent.</param>
    /// <param name="workflowChanges">The immutable proposals prepared from the current workflow and observed inputs.</param>
    /// <param name="workflowTransition">The accepted proposals or business rejection.</param>
    /// <returns>True when every proposed workflow snapshot belongs to the command.</returns>
    internal static bool Compute(this ExecuteIntrinsicTimeStrategyWorkflowCommand command, IReadOnlyList<WorkflowSnapshotChange> workflowChanges, out WorkflowSnapshotTransition workflowTransition)
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
    internal static IReadOnlyList<WorkflowStrategyStateUpdatedEvent> CreateWorkflowLifecycleEvents(this ExecuteIntrinsicTimeStrategyWorkflowCommand command, WorkflowSnapshotTransition workflowTransition)
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
