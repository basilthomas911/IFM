using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
/// <summary>Owns ScheduledTaskCatalog state reconstructed exclusively from persisted source events.</summary>
public sealed class ScheduledTaskCatalogCommandState : BaseEventSourceActorState<ScheduledTaskCatalogCommandState>
{
    /// <inheritdoc />
    public override ActorThreadId Id { get; set; } = default!;
    /// <summary>Gets the current business aggregate; only Apply mutates it.</summary>
    public ScheduledTaskCatalog? ScheduledTaskCatalog { get; private set; }
    /// <summary>Dispatches concrete source events to the owned aggregate.</summary>
    protected override bool Apply(IEvent domainEvent) => domainEvent switch
    {
        ScheduledTaskProjectRegisteredEvent changed => ApplyScheduledTaskCatalog(changed.EntityId, changed.ScheduledTaskCatalog),
        ScheduledTaskHostCapabilityRecordedEvent changed => ApplyScheduledTaskCatalog(changed.EntityId, changed.ScheduledTaskCatalog),
        _ => false
    };
    /// <summary>Accepts only an event for this identity with the next consecutive revision.</summary>
    private bool ApplyScheduledTaskCatalog(ScheduledTaskId identity, ScheduledTaskCatalog? change)
    {
        if (!identity.IsValid || change is null || change.Id != identity ||
            change.Revision != (ScheduledTaskCatalog?.Revision ?? 0) + 1) return false;
        ScheduledTaskCatalog = change;
        return true;
    }
}
