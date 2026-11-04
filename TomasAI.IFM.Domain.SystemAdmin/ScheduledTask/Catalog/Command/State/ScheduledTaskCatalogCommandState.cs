using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Catalog.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Catalog.Command.State;
/// <summary>Owns ScheduledTaskCatalog state reconstructed exclusively from persisted source events.</summary>
public sealed class ScheduledTaskCatalogCommandState : BaseEventSourceActorState<ScheduledTaskCatalogCommandState>
{
    /// <inheritdoc />
    public override ActorThreadId Id { get; set; } = default!;
    /// <summary>Gets the current business aggregate; only Apply mutates it.</summary>
    public ScheduledTaskCatalog? ScheduledTaskCatalog { get; private set; }
    /// <summary>Dispatches concrete source events to the owned aggregate.</summary>
    protected override bool Apply(IEvent domainEvent) => ApplyCheckpointEvent(domainEvent, false, replay: false);

    /// <summary>Allows a full persisted replacement to resume at a later revision during checkpoint replay.</summary>
    internal bool ApplyCheckpointEvent(IEvent domainEvent, bool snapshot, bool replay = true) => domainEvent switch
    {
        ScheduledTaskCatalogProjectRegisteredEvent changed => ApplyScheduledTaskCatalog(changed.EntityId, changed.ScheduledTaskCatalog, snapshot || (replay && ScheduledTaskCatalog is null)),
        ScheduledTaskCatalogHostCapabilityRecordedEvent changed => ApplyScheduledTaskCatalog(changed.EntityId, changed.ScheduledTaskCatalog, snapshot),
        _ => false
    };
    /// <summary>Restores an initial registered-project snapshot at its persisted revision during replay only.</summary>
    protected override bool ApplyReplayEvent(IEvent domainEvent) => ApplyCheckpointEvent(domainEvent, false);

    /// <summary>Requires consecutive revisions outside checkpoint restoration.</summary>
    private bool ApplyScheduledTaskCatalog(ScheduledTaskId identity, ScheduledTaskCatalog? change, bool initialSnapshot = false)
    {
        if (!identity.IsValid || change is null || change.Id != identity ||
            ScheduledTaskCatalog is { } current && current.Id != identity ||
            change.Revision <= (ScheduledTaskCatalog?.Revision ?? 0) ||
            (!initialSnapshot && change.Revision != (ScheduledTaskCatalog?.Revision ?? 0) + 1)) return false;
        ScheduledTaskCatalog = change;
        return true;
    }
}
