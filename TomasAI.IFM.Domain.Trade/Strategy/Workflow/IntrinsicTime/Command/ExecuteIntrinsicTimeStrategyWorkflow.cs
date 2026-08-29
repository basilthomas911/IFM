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

/// <summary>Handles workflow execution admission.</summary>
public static class ExecuteIntrinsicTimeStrategyWorkflow
{
    /// <summary>Starts a free workflow or atomically expires and replaces an overdue workflow.</summary>
    public static ServiceResult<GuidResult> Execute(
        this ExecuteIntrinsicTimeStrategyWorkflowCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IntrinsicTimeStrategyWorkflowCommandState state)
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

        if (current is { Status: WorkflowStrategyMachineStatus.Started } && now < current.ExpiresAtUtc)
        {
            context.Logger.LogWarning(
                "Workflow Execute rejected as busy for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
                command.EntityId.Format(), current.WorkflowId, current.WorkflowRevision);
            return Ok(command);
        }

        if (current is { Status: WorkflowStrategyMachineStatus.Started })
        {
            var expired = CreateExpiredView(current, command.CommandId, now);
            AppendSnapshot(state, command, current.Status, expired, now);
            context.Logger.LogWarning(
                "Expired workflow {ExpiredWorkflowId} was lazily closed and replaced by {WorkflowId} for {WorkflowEntityId}",
                current.WorkflowId, command.ProposedWorkflowId, command.EntityId.Format());
            current = expired;
        }

        var expiresAtUtc = now.Add(maximumExecutionDuration);
        var parameterSet = command.RegimeDiscoveryParameterSet;
        var marketConditionParameterSet = command.MarketConditionParameterSet;
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
                ParameterSetId = parameterSet.ParameterSetId,
                ParameterSetVersion = parameterSet.Version,
                ParameterPayloadSha256 = command.RegimeDiscoveryParameterPayloadSha256,
                ExpiresAtUtc = expiresAtUtc
            },
            RegimeDiscoveryParameterSet = parameterSet,
            RegimeDiscoveryParameterPayloadSha256 = command.RegimeDiscoveryParameterPayloadSha256,
            FundId = command.FundId,
            MarketConditionParameterSet = marketConditionParameterSet,
            MarketConditionParameterPayloadSha256 = command.MarketConditionParameterPayloadSha256,
            Outcome = StrategyWorkflowOutcome.None,
            TriggerEvent = command.TriggerEvent
        };
        AppendSnapshot(state, command, current?.Status ?? WorkflowStrategyMachineStatus.Empty, started, now);
        return Ok(command);
    }

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

    static void AppendSnapshot(
        IntrinsicTimeStrategyWorkflowCommandState state,
        ExecuteIntrinsicTimeStrategyWorkflowCommand command,
        WorkflowStrategyMachineStatus previousStatus,
        IntrinsicTimeStrategyWorkflowView view,
        DateTime now)
        => state.Update(new WorkflowStrategyStateUpdatedEvent
        {
            Subject = new ActorSubject(ActorType.Event, WorkflowStrategyStateUpdatedEvent.Actor,
                WorkflowStrategyStateUpdatedEvent.Verb, command.EntityId.Format()),
            Id = Guid.CreateVersion7(new DateTimeOffset(now, TimeSpan.Zero)),
            EntityId = command.EntityId,
            CommandId = command.CommandId,
            AggregateId = command.EntityId.Format(),
            EventSource = command.EventSource,
            ReceivedOn = now,
            WorkflowId = view.WorkflowId,
            WorkflowRevision = view.WorkflowRevision,
            CorrelationId = view.CorrelationId,
            CausationId = view.CausationId,
            PreviousStatus = previousStatus,
            State = view,
            UpdatedAtUtc = now
        }, command);

    static ServiceResult<GuidResult> Ok(ExecuteIntrinsicTimeStrategyWorkflowCommand command)
        => new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
}
