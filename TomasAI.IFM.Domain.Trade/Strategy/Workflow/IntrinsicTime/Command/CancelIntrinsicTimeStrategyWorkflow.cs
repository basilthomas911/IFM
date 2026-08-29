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

/// <summary>Handles explicit workflow cancellation.</summary>
public static class CancelIntrinsicTimeStrategyWorkflow
{
    /// <summary>Marks the current workflow stage and workflow as cancelled.</summary>
    public static ServiceResult<GuidResult> Execute(this CancelIntrinsicTimeStrategyWorkflowCommand command,
        ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context,
        IntrinsicTimeStrategyWorkflowCommandState state)
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
                "Stale or duplicate workflow terminal command {CommandName} ignored for {WorkflowEntityId} {WorkflowId} revision {WorkflowRevision}",
                command.CommandName, command.Subject.EntityId, current?.WorkflowId, current?.WorkflowRevision);
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
        state.Update(new WorkflowStrategyStateUpdatedEvent
        {
            Subject = new ActorSubject(ActorType.Event, WorkflowStrategyStateUpdatedEvent.Actor,
                WorkflowStrategyStateUpdatedEvent.Verb, command.EntityId.Format()),
            Id = Guid.CreateVersion7(new DateTimeOffset(now, TimeSpan.Zero)),
            EntityId = command.EntityId,
            CommandId = command.CommandId,
            AggregateId = command.EntityId.Format(),
            EventSource = command.EventSource,
            ReceivedOn = now,
            WorkflowId = cancelled.WorkflowId,
            WorkflowRevision = cancelled.WorkflowRevision,
            CorrelationId = cancelled.CorrelationId,
            CausationId = cancelled.CausationId,
            PreviousStatus = current.Status,
            State = cancelled,
            UpdatedAtUtc = now
        }, command);
        return Ok(command);
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

    static ServiceResult<GuidResult> Ok(CancelIntrinsicTimeStrategyWorkflowCommand command)
        => new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
}
