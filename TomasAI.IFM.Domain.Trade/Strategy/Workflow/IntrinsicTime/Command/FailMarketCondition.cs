using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;

/// <summary>Handles failed Market Condition completion.</summary>
public static class FailMarketCondition
{
    /// <summary>Records the Market Condition failure or timeout and closes the workflow.</summary>
    public static ServiceResult<GuidResult> Execute(this FailMarketConditionCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IntrinsicTimeStrategyWorkflowCommandState state)
    {
        ArgumentNullException.ThrowIfNull(command); ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(state);
        var current = state.CurrentView;
        if (current is not { Status: WorkflowStrategyMachineStatus.Started } ||
            current.WorkflowId != command.WorkflowId || current.WorkflowRevision != command.InputWorkflowRevision ||
            current.CurrentStage != StrategyWorkflowStage.MarketCondition ||
            current.MarketCondition.SourceEventId == command.SourceEventId)
        {
            LogStale(context, command, current); return Ok(command);
        }
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        var timedOut = now >= current.ExpiresAtUtc ||
            command.FailureCategory == MarketConditionFailureCategory.Timeout ||
            IsLegacyTimeout(command);
        var updated = current with
        {
            Status = timedOut ? WorkflowStrategyMachineStatus.TimedOut : WorkflowStrategyMachineStatus.Failed,
            Outcome = timedOut ? StrategyWorkflowOutcome.TimedOut : StrategyWorkflowOutcome.PipelineFailed,
            WorkflowRevision = current.WorkflowRevision + 1, CausationId = command.CausationId,
            UpdatedAtUtc = now, TerminalAtUtc = now,
            StopReasonCode = timedOut ? "PipelineTimedOut" : command.Failure.ErrorCode.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            MarketCondition = current.MarketCondition with
            {
                ProcessingStatus = timedOut ? StrategyActorProcessingStatus.TimedOut : StrategyActorProcessingStatus.Failed,
                FailedAtUtc = now, Failure = command.Failure, SourceEventId = command.SourceEventId
            }
        };
        AppendSnapshot(state, command, current.Status, updated, now);
        if (timedOut) LogDeadline(context, command, updated);
        return Ok(command);
    }

    // Compatibility is limited to pre-V1 commands which did not carry a typed category.
    static bool IsLegacyTimeout(FailMarketConditionCommand command) =>
        command.FailureCategory == MarketConditionFailureCategory.Undefined &&
        (command.Failure.ErrorCode == 23103 ||
         command.Failure.ErrorType.Contains("Timeout", StringComparison.OrdinalIgnoreCase) ||
         command.Failure.ErrorType.Contains("TimedOut", StringComparison.OrdinalIgnoreCase));

    static void AppendSnapshot(IntrinsicTimeStrategyWorkflowCommandState state,
        FailMarketConditionCommand command, WorkflowStrategyMachineStatus previousStatus,
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

    static void LogStale(ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        FailMarketConditionCommand command, IntrinsicTimeStrategyWorkflowView? current)
        => context.Logger.LogWarning("Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
            command.CommandName, command.Subject.EntityId, current?.WorkflowId, current?.WorkflowRevision);

    static void LogDeadline(ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        FailMarketConditionCommand command, IntrinsicTimeStrategyWorkflowView view)
        => context.Logger.LogWarning("Workflow deadline took precedence for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
            command.Subject.EntityId, view.WorkflowId, view.WorkflowRevision);

    static ServiceResult<GuidResult> Ok(FailMarketConditionCommand command)
        => new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
}
