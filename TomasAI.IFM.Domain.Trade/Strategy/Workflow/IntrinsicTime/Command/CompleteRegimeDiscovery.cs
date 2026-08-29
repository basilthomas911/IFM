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

/// <summary>Handles successful Regime Discovery completion.</summary>
public static class CompleteRegimeDiscovery
{
    /// <summary>Records the Regime result and selects Market Condition.</summary>
    public static ServiceResult<GuidResult> Execute(
        this CompleteRegimeDiscoveryCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IntrinsicTimeStrategyWorkflowCommandState state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);

        var current = state.CurrentView;
        if (current is not { Status: WorkflowStrategyMachineStatus.Started } ||
            current.WorkflowId != command.WorkflowId ||
            current.WorkflowRevision != command.InputWorkflowRevision ||
            current.CurrentStage != StrategyWorkflowStage.RegimeDiscovery ||
            current.RegimeDiscovery.SourceEventId == command.SourceEventId)
        {
            LogStale(context, command, current);
            return Ok(command);
        }

        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        if (now >= current.ExpiresAtUtc)
        {
            var failure = TimeoutFailure(now);
            var timedOut = current with
            {
                Status = WorkflowStrategyMachineStatus.TimedOut,
                WorkflowRevision = current.WorkflowRevision + 1,
                CausationId = command.SourceEventId,
                UpdatedAtUtc = now,
                TerminalAtUtc = now,
                StopReasonCode = "WorkflowExecutionExpired",
                RegimeDiscovery = current.RegimeDiscovery with
                {
                    ProcessingStatus = StrategyActorProcessingStatus.TimedOut,
                    FailedAtUtc = now,
                    Failure = failure,
                    SourceEventId = command.SourceEventId
                }
            };
            AppendSnapshot(state, command, current.Status, timedOut, now);
            LogDeadline(context, command, timedOut);
            return Ok(command);
        }

        var revision = current.WorkflowRevision + 1;
        var updated = current with
        {
            CausationId = command.CausationId,
            WorkflowRevision = revision,
            UpdatedAtUtc = now,
            CurrentStage = StrategyWorkflowStage.MarketCondition,
            RegimeDiscovery = current.RegimeDiscovery with
            {
                ProcessingStatus = StrategyActorProcessingStatus.Completed,
                ContinuationDecision = StrategyWorkflowContinuationDecision.Proceed,
                CompletedAtUtc = now,
                FailedAtUtc = null,
                Result = command.Result,
                Failure = null,
                SourceEventId = command.SourceEventId,
                ContinuationRuleSetId = "IntrinsicTimeStrategyWorkflow.v1",
                ContinuationRuleSetVersion = 1,
                ContinuationReasonCodes = []
            },
            MarketCondition = new StrategyWorkflowStageState
            {
                ProcessingStatus = StrategyActorProcessingStatus.Processing,
                StartedAtUtc = now,
                InputWorkflowRevision = revision,
                ParameterSetId = current.MarketConditionParameterSet.ParameterSetId,
                ParameterSetVersion = current.MarketConditionParameterSet.Version,
                ParameterPayloadSha256 = current.MarketConditionParameterPayloadSha256,
                ExpiresAtUtc = current.ExpiresAtUtc
            }
        };
        AppendSnapshot(state, command, current.Status, updated, now);
        return Ok(command);
    }

    static void AppendSnapshot(IntrinsicTimeStrategyWorkflowCommandState state,
        CompleteRegimeDiscoveryCommand command, WorkflowStrategyMachineStatus previousStatus,
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
        CompleteRegimeDiscoveryCommand command, IntrinsicTimeStrategyWorkflowView? current)
        => context.Logger.LogWarning(
            "Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
            command.CommandName, command.Subject.EntityId, current?.WorkflowId, current?.WorkflowRevision);

    static void LogDeadline(ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        CompleteRegimeDiscoveryCommand command, IntrinsicTimeStrategyWorkflowView view)
        => context.Logger.LogWarning(
            "Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
            command.Subject.EntityId, view.WorkflowId, view.WorkflowRevision);

    static ServiceResult<GuidResult> Ok(CompleteRegimeDiscoveryCommand command)
        => new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
}
