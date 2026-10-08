using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events;
using TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Event;
/// <summary>Creates host-routed runtime requests from committed scheduled-task business events.</summary>
public static class ScheduledTaskRuntimeDispatch
{
    /// <summary>Publishes a committed desired definition to the host-specific mailbox.</summary>
    public static ValueTask ApplyAsync(IEvent source, ScheduledTaskDefinition definition, Guid operationCommandId, ScheduledTaskEventContext owner)
    {
        var request = new ApplyScheduledTaskRuntimeEvent
        {
            Id = source.Id, CommandId = source.CommandId, EntityId = definition.Id, AggregateId = definition.Id.Format(),
            OperationCommandId = operationCommandId, ScheduledTaskDefinition = definition,
            Subject = new(ActorType.Event, ScheduledTaskIdentities.RuntimeActor(definition.Schedule.Environment, definition.Schedule.HostId), ApplyScheduledTaskRuntimeEvent.Verb, definition.Id.Format())
        };
        return owner.Producer.SendAsync<ApplyScheduledTaskRuntimeEvent, ScheduledTaskId>(request.Subject, request);
    }
    /// <summary>Requests one manual occurrence only after its run source event and projection are committed.</summary>
    public static ValueTask RequestManualRunAsync(ScheduledTaskRunRequestedCompleteEvent source, ScheduledTaskRun run, ScheduledTaskEventContext owner)
    {
        var request = new ExecuteScheduledTaskRuntimeRunEvent
        {
            Id = source.Id, CommandId = source.CommandId, EntityId = run.Id, AggregateId = run.Id.Format(),
            OperationCommandId = source.OperationCommandId, ScheduledTaskRun = run,
            Subject = new(ActorType.Event, ScheduledTaskIdentities.RuntimeActor(run.Environment, run.HostId), ExecuteScheduledTaskRuntimeRunEvent.Verb, run.Id.Format())
        };
        return owner.Producer.SendAsync<ExecuteScheduledTaskRuntimeRunEvent, ScheduledTaskId>(request.Subject, request);
    }
}
