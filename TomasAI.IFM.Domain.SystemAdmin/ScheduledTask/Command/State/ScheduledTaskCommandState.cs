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
    protected override bool Apply(IEvent domainEvent) => ApplyCheckpointEvent(domainEvent, false);

    /// <summary>Allows a full persisted replacement to resume at a later revision during checkpoint replay.</summary>
    internal bool ApplyCheckpointEvent(IEvent domainEvent, bool snapshot) => domainEvent switch
    {
        ScheduledTaskCreatedEvent changed => ApplyScheduledTask(changed.EntityId, changed.ScheduledTaskDefinition, snapshot),
        ScheduledTaskScheduleChangedEvent changed => ApplyScheduledTask(changed.EntityId, changed.ScheduledTaskDefinition, snapshot),
        ScheduledTaskEnabledEvent changed => ApplyScheduledTask(changed.EntityId, changed.ScheduledTaskDefinition, snapshot),
        ScheduledTaskDisabledEvent changed => ApplyScheduledTask(changed.EntityId, changed.ScheduledTaskDefinition, snapshot),
        ScheduledTaskRemovedEvent changed => ApplyScheduledTask(changed.EntityId, changed.ScheduledTaskDefinition, snapshot),
        ScheduledTaskInstallationRecordedEvent changed => ApplyScheduledTask(changed.EntityId, changed.ScheduledTaskDefinition, snapshot),
        ScheduledTaskInstallationFailureRecordedEvent changed => ApplyScheduledTask(changed.EntityId, changed.ScheduledTaskDefinition, snapshot),
        ScheduledTaskRunAdmittedEvent changed => ApplyScheduledTask(changed.EntityId, changed.ScheduledTaskDefinition, snapshot),
        ScheduledTaskRunReleasedEvent changed => ApplyScheduledTask(changed.EntityId, changed.ScheduledTaskDefinition, snapshot),
        _ => false
    };
    /// <summary>Requires consecutive revisions outside checkpoint restoration.</summary>
    private bool ApplyScheduledTask(ScheduledTaskId identity, ScheduledTaskDefinition? change, bool snapshot)
    {
        if (!identity.IsValid || change is null || change.Id != identity ||
            ScheduledTaskDefinition is { } current && current.Id != identity ||
            change.Revision <= (ScheduledTaskDefinition?.Revision ?? 0) || (!snapshot && change.Revision != (ScheduledTaskDefinition?.Revision ?? 0) + 1)) return false;
        ScheduledTaskDefinition = change;
        return true;
    }
}
