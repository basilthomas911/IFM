using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.State;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Extensions;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command;
/// <summary>Republishes the durable current intent. Recovery never changes an execution identity, input revision or deadline.</summary>
public static class RedispatchCurrentStrategyPipeline
{
    public static ServiceResult<GuidResult> Execute(this RedispatchCurrentStrategyPipelineCommand c, ICommandActorContext<IntrinsicTimeStrategyWorkflowCommandActor> context, IntrinsicTimeStrategyWorkflowCommandState state)
    {
        var view = state.CurrentView;
        if (view is null || view.WorkflowId != c.WorkflowId || view.WorkflowRevision != c.ExpectedWorkflowRevision || view.CurrentStage != c.ExpectedStage)
            return new ServiceOk<GuidResult>(new(c.CommandId));
        if (view.Status != WorkflowStrategyMachineStatus.Started && view.CompositionHandoff is null)
            return new ServiceOk<GuidResult>(new(c.CommandId));
        var now = context.TimeProvider.GetUtcNow().UtcDateTime;
        state.Update(new WorkflowStrategyStateUpdatedEvent
        {
            Subject = new(ActorType.Event, WorkflowStrategyStateUpdatedEvent.Actor, WorkflowStrategyStateUpdatedEvent.Verb, c.EntityId.Format()),
            Id = Guid.CreateVersion7(new DateTimeOffset(now)),
            EntityId = c.EntityId,
            CommandId = c.CommandId,
            AggregateId = c.EntityId.Format(),
            EventSource = c.EventSource,
            ReceivedOn = now,
            WorkflowId = view.WorkflowId,
            WorkflowRevision = view.WorkflowRevision,
            CorrelationId = view.CorrelationId,
            CausationId = c.CommandId,
            PreviousStatus = view.Status,
            State = view,
            UpdatedAtUtc = now
        }, c);
        return new ServiceOk<GuidResult>(new(c.CommandId));
    }
}
