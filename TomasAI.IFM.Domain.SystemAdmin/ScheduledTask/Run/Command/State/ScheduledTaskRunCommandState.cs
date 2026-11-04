using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Run.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Run.Command.State;
/// <summary>Owns ScheduledTaskRun state reconstructed exclusively from persisted source events.</summary>
public sealed class ScheduledTaskRunCommandState : BaseEventSourceActorState<ScheduledTaskRunCommandState>
{
    /// <inheritdoc />
    public override ActorThreadId Id { get; set; } = default!;
    /// <summary>Gets the current business aggregate; only Apply mutates it.</summary>
    public ScheduledTaskRun? ScheduledTaskRun { get; private set; }
    /// <summary>Dispatches concrete source events to the owned aggregate.</summary>
    protected override bool Apply(IEvent domainEvent) => ApplyCheckpointEvent(domainEvent, false);

    /// <summary>Allows a full persisted replacement to resume at a later revision during checkpoint replay.</summary>
    internal bool ApplyCheckpointEvent(IEvent domainEvent, bool snapshot) => domainEvent switch
    {
        ScheduledTaskRunRequestedEvent changed => ApplyScheduledTaskRun(changed.EntityId, changed.ScheduledTaskRun, snapshot),
        ScheduledTaskRunAdmissionRecordedEvent changed => ApplyScheduledTaskRun(changed.EntityId, changed.ScheduledTaskRun, snapshot),
        ScheduledTaskRunStageRecordedEvent changed => ApplyScheduledTaskRun(changed.EntityId, changed.ScheduledTaskRun, snapshot),
        ScheduledTaskRunStartedEvent changed => ApplyScheduledTaskRun(changed.EntityId, changed.ScheduledTaskRun, snapshot),
        ScheduledTaskRunCompletedEvent changed => ApplyScheduledTaskRun(changed.EntityId, changed.ScheduledTaskRun, snapshot),
        ScheduledTaskRunFailedEvent changed => ApplyScheduledTaskRun(changed.EntityId, changed.ScheduledTaskRun, snapshot),
        ScheduledTaskRunUncertainRecordedEvent changed => ApplyScheduledTaskRun(changed.EntityId, changed.ScheduledTaskRun, snapshot),
        _ => false
    };
    /// <summary>Requires consecutive revisions outside checkpoint restoration.</summary>
    private bool ApplyScheduledTaskRun(ScheduledTaskId identity, ScheduledTaskRun? change, bool snapshot)
    {
        if (!identity.IsValid || change is null || change.Id != identity ||
            ScheduledTaskRun is { } current && current.Id != identity ||
            change.Revision <= (ScheduledTaskRun?.Revision ?? 0) || (!snapshot && change.Revision != (ScheduledTaskRun?.Revision ?? 0) + 1)) return false;
        ScheduledTaskRun = change;
        return true;
    }
}
