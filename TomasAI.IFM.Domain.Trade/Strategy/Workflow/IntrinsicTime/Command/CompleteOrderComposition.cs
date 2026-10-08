using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
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

/// <summary>Handles successful Order Composition completion.</summary>
public static class CompleteOrderComposition
{
    /// <summary>Verifies selected legs against the accepted immutable snapshot before the actor appends completion.</summary>
    /// <param name="command">The concrete command intent.</param>
    /// <param name="context">The authorized actor services and logging context.</param>
    /// <param name="state">The authoritative state or local preparation snapshot, as declared by the method.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    public static ValueTask<ServiceResult<GuidResult>> ExecutePreparedAsync(this CompleteOrderCompositionCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context, IntrinsicTimeStrategyWorkflowCommandState state)
        => ValueTask.FromResult(command.Execute(context, state));

    /// <summary>Records the Order Composition result and selects Risk Management.</summary>
    /// <param name="command">The concrete workflow command.</param>
    /// <param name="context">The authorized workflow services, clock, and logger.</param>
    /// <param name="state">The authoritative workflow state.</param>
    /// <returns>The command acceptance or rejection.</returns>
    public static ServiceResult<GuidResult> Execute(this CompleteOrderCompositionCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IntrinsicTimeStrategyWorkflowCommandState state)
    {
        var preparation = new WorkflowSnapshotPreparation(state.WorkflowDefinition);
        var preparationResult = PrepareWorkflow(command, context, preparation);
        if (!preparationResult.Success) return preparationResult;
        var errorMsg = "IntrinsicTimeStrategyWorkflow.STATE.APPLY_FAILED: unable to apply CompleteOrderComposition event";
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
    internal static ServiceResult<GuidResult> PrepareWorkflow(this CompleteOrderCompositionCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        WorkflowSnapshotPreparation state)
    {
        ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        var current = state.CurrentView;
        if (current is not { Status: WorkflowStrategyMachineStatus.Started } ||
            current.WorkflowId != command.WorkflowId || current.WorkflowRevision != command.InputWorkflowRevision ||
            current.CurrentStage != StrategyWorkflowStage.OrderComposition ||
            current.OrderComposition.SourceEventId == command.SourceEventId)
        {
            context.Logger.LogWarning("{Component}.{Method} "+"Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",nameof(CompleteOrderComposition),nameof(Execute),                command.CommandName,command.Subject.EntityId,current?.WorkflowId,current?.WorkflowRevision);
            return Ok(command);
        }
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        if (now >= current.ExpiresAtUtc || current.CompositionExecution is { } execution && now >= execution.ExpiresAtUtc)
        {
            var failure = TimeoutFailure(now);
            var timedOut = current with
            {
                Status = WorkflowStrategyMachineStatus.TimedOut,
                WorkflowRevision = current.WorkflowRevision + 1,
                CausationId = command.SourceEventId,
                UpdatedAtUtc = now,
                TerminalAtUtc = now,
                StopReasonCode = now >= current.ExpiresAtUtc ? "WorkflowExecutionExpired" : "OC.TIME.EXPIRED",
                OrderComposition = current.OrderComposition with
                {
                    ProcessingStatus = StrategyActorProcessingStatus.TimedOut,
                    FailedAtUtc = now,
                    Failure = failure,
                    SourceEventId = command.SourceEventId
                }
            };
            AppendSnapshot(state, command, current.Status, timedOut, now);
            context.Logger.LogWarning("{Component}.{Method} "+"Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",nameof(CompleteOrderComposition),nameof(Execute),                command.Subject.EntityId,timedOut.WorkflowId,timedOut.WorkflowRevision);
            return Ok(command);
        }
        OrderCompositionResult result;
        try
        {
            var accepted = current.CompositionExecution ?? throw new CompositionException("OC.RESULT.LEGACY_READ_ONLY");
            using var deadline = new CancellationTokenSource(accepted.ExpiresAtUtc > now ? accepted.ExpiresAtUtc - now : TimeSpan.Zero);
            result = CompositionAcceptance.Validate(accepted, command, new TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model.OrderComposer(new Black76ComposerPricer()), now, deadline.Token);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OperationCanceledException)
        {
            var expired = ex is OperationCanceledException || ex is CompositionException { ReasonCode: "OC.TIME.EXPIRED" };
            var invalid = current with
            {
                Status = expired ? WorkflowStrategyMachineStatus.TimedOut : WorkflowStrategyMachineStatus.Failed,
                Outcome = StrategyWorkflowOutcome.PipelineFailed,
                WorkflowRevision = current.WorkflowRevision + 1,
                UpdatedAtUtc = now,
                TerminalAtUtc = now,
                StopReasonCode = expired ? "OC.TIME.EXPIRED" : "OC.RESULT.INVALID",
                OrderComposition = current.OrderComposition with
                {
                    ProcessingStatus = expired ? StrategyActorProcessingStatus.TimedOut : StrategyActorProcessingStatus.Failed,
                    FailedAtUtc = now,
                    SourceEventId = command.SourceEventId,
                    Failure = new()
                    {
                        ErrorCode = 23024,
                        ErrorType = expired ? "OrderCompositionTimedOut" : "OrderCompositionResultInvalid",
                        ErrorMessage = expired ? "Composition validity expired before acceptance." : "Composition result failed immutable-input verification.",
                        FailedAtUtc = now
                    }
                }
            };
            AppendSnapshot(state, command, current.Status, invalid, now); return Ok(command);
        }
        var noCandidate = result.Outcome == CompositionOutcome.NoCandidate;
        var revision = current.WorkflowRevision + 1;
        var updated = current with
        {
            CausationId = command.CausationId,
            WorkflowRevision = revision,
            UpdatedAtUtc = now,
            CurrentStage = noCandidate ? StrategyWorkflowStage.OrderComposition : StrategyWorkflowStage.RiskManagement,
            Status = noCandidate ? WorkflowStrategyMachineStatus.Completed : WorkflowStrategyMachineStatus.Started,
            Outcome = noCandidate ? StrategyWorkflowOutcome.NoTrade : StrategyWorkflowOutcome.None,
            TerminalAtUtc = noCandidate ? now : null,
            StopReasonCode = noCandidate ? result.Reasons[0] : string.Empty,
            CompositionContracts = command.SelectedContracts,
            OrderComposition = current.OrderComposition with
            {
                ProcessingStatus = StrategyActorProcessingStatus.Completed,
                ContinuationDecision = noCandidate ? StrategyWorkflowContinuationDecision.Stop : StrategyWorkflowContinuationDecision.Proceed,
                CompletedAtUtc = now,
                FailedAtUtc = null,
                Result = command.Result,
                Failure = null,
                SourceEventId = command.SourceEventId,
                ContinuationRuleSetId = "IntrinsicTimeStrategyWorkflow.v1",
                ContinuationRuleSetVersion = 1,
                ContinuationReasonCodes = []
            },
            RiskManagement = noCandidate ? current.RiskManagement : new StrategyWorkflowStageState
            {
                ProcessingStatus = StrategyActorProcessingStatus.Processing,
                StartedAtUtc = now,
                InputWorkflowRevision = revision,
                ExpiresAtUtc = current.ExpiresAtUtc
            }
        };
        AppendSnapshot(state, command, current.Status, updated, now);
        return Ok(command);
    }

    /// <summary>Evaluates append snapshot business information.</summary>
    /// <param name="state">The authoritative state or local preparation snapshot, as declared by the method.</param>
    /// <param name="command">The concrete command intent.</param>
    /// <param name="previousStatus">The lifecycle status before this transition.</param>
    /// <param name="view">The immutable workflow snapshot.</param>
    /// <param name="now">The observed UTC time used for deadline and lifecycle decisions.</param>
    static void AppendSnapshot(WorkflowSnapshotPreparation state,
        CompleteOrderCompositionCommand command, WorkflowStrategyMachineStatus previousStatus,
        IntrinsicTimeStrategyWorkflowView view, DateTime now)
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

    /// <summary>Evaluates timeout failure business information.</summary>
    /// <param name="now">The observed UTC time used for deadline and lifecycle decisions.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    static StrategyPipelineFailure TimeoutFailure(DateTime now) => new()
    {
        ErrorCode = Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events.OrderCompositionFunctionFailedEvent.ErrorId,
        ErrorMessage = "The fixed composition or workflow execution deadline was reached.",
        ErrorType = "OrderCompositionTimedOut",
        FailedAtUtc = now
    };

    /// <summary>Evaluates ok business information.</summary>
    /// <param name="command">The concrete command intent.</param>
    /// <returns>The prepared business result or command acceptance.</returns>
    static ServiceResult<GuidResult> Ok(CompleteOrderCompositionCommand command)
        => new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
    /// <summary>Checks ownership and required data of prepared workflow changes without mutation.</summary>
    /// <param name="command">The concrete workflow intent.</param>
    /// <param name="workflowChanges">The immutable proposals prepared from the current workflow and observed inputs.</param>
    /// <param name="workflowTransition">The accepted proposals or business rejection.</param>
    /// <returns>True when every proposed workflow snapshot belongs to the command.</returns>
    internal static bool Compute(this CompleteOrderCompositionCommand command, IReadOnlyList<WorkflowSnapshotChange> workflowChanges, out WorkflowSnapshotTransition workflowTransition)
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
    internal static IReadOnlyList<WorkflowStrategyStateUpdatedEvent> CreateWorkflowLifecycleEvents(this CompleteOrderCompositionCommand command, WorkflowSnapshotTransition workflowTransition)
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
