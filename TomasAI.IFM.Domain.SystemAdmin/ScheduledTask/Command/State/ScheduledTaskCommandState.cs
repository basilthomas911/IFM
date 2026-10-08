using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
/// <summary>Owns ScheduledTask state reconstructed exclusively from persisted source events.</summary>
public sealed class ScheduledTaskCommandState : BaseEventSourceActorState<ScheduledTaskCommandState>
{
    /// <inheritdoc />
    public override ActorThreadId Id { get; set; } = default!;
    /// <summary>Gets the current business aggregate; only Apply mutates it.</summary>
    public ScheduledTaskDefinition? ScheduledTaskDefinition { get; private set; }
    /// <summary>Dispatches concrete source events to the owned aggregate.</summary>
    protected override bool Apply(IEvent domainEvent) => domainEvent switch
    {
        ScheduledTaskCreatedEvent changed => ApplyScheduledTask(changed.EntityId, changed.ScheduledTaskDefinition),
        ScheduledTaskScheduleChangedEvent changed => ApplyScheduledTask(changed.EntityId, changed.ScheduledTaskDefinition),
        ScheduledTaskEnabledEvent changed => ApplyScheduledTask(changed.EntityId, changed.ScheduledTaskDefinition),
        ScheduledTaskDisabledEvent changed => ApplyScheduledTask(changed.EntityId, changed.ScheduledTaskDefinition),
        ScheduledTaskRemovedEvent changed => ApplyScheduledTask(changed.EntityId, changed.ScheduledTaskDefinition),
        ScheduledTaskInstallationRecordedEvent changed => ApplyScheduledTask(changed.EntityId, changed.ScheduledTaskDefinition),
        ScheduledTaskInstallationFailureRecordedEvent changed => ApplyScheduledTask(changed.EntityId, changed.ScheduledTaskDefinition),
        ScheduledTaskRunAdmittedEvent changed => ApplyScheduledTask(changed.EntityId, changed.ScheduledTaskDefinition),
        ScheduledTaskRunReleasedEvent changed => ApplyScheduledTask(changed.EntityId, changed.ScheduledTaskDefinition),
        _ => false
    };
    /// <summary>Accepts only an event for this identity with the next consecutive revision.</summary>
    private bool ApplyScheduledTask(ScheduledTaskId identity, ScheduledTaskDefinition? change)
    {
        if (!identity.IsValid || change is null || change.Id != identity ||
            change.Revision != (ScheduledTaskDefinition?.Revision ?? 0) + 1) return false;
        ScheduledTaskDefinition = change;
        return true;
    }
}
