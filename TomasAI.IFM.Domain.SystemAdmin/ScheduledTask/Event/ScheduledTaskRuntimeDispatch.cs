using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
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
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(source);
        var request = new ApplyScheduledTaskRuntimeEvent
        {
            Id = source.Id, CommandId = source.CommandId, EntityId = definition.Id, AggregateId = definition.Id.Format(),
            OperationCommandId = operationCommandId, ScheduledTaskDefinition = definition,
            Subject = new(ActorType.Event, ScheduledTaskIdentities.RuntimeActor(definition.Schedule.Environment, definition.Schedule.HostId), ApplyScheduledTaskRuntimeEvent.Verb, definition.Id.Format())
        };
        return owner.Producer.SendAsync<ApplyScheduledTaskRuntimeEvent, ScheduledTaskId>(request.Subject, request);
    }
    /// <summary>Creates the same host-specific manual request on every projection attempt.</summary>
    public static ExecuteScheduledTaskRuntimeRunEvent CreateManualRunRequest(IEvent source, ScheduledTaskRun run, Guid operationCommandId)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(source);
        return new ExecuteScheduledTaskRuntimeRunEvent
        {
            Id = source.Id, CommandId = source.CommandId, EntityId = run.Id, AggregateId = run.Id.Format(),
            OperationCommandId = operationCommandId, ScheduledTaskRun = run,
            Subject = new(ActorType.Event, ScheduledTaskIdentities.RuntimeActor(run.Environment, run.HostId), ExecuteScheduledTaskRuntimeRunEvent.Verb, run.Id.Format())
        };
    }
}
