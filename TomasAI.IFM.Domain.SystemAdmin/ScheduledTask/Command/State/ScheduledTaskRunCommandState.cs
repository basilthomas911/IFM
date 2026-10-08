using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Events.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.State;
/// <summary>Owns ScheduledTaskRun state reconstructed exclusively from persisted source events.</summary>
public sealed class ScheduledTaskRunCommandState : BaseEventSourceActorState<ScheduledTaskRunCommandState>
{
    /// <inheritdoc />
    public override ActorThreadId Id { get; set; } = default!;
    /// <summary>Gets the current business aggregate; only Apply mutates it.</summary>
    public ScheduledTaskRun? ScheduledTaskRun { get; private set; }
    /// <summary>Dispatches concrete source events to the owned aggregate.</summary>
    protected override bool Apply(IEvent domainEvent) => domainEvent switch
    {
        ScheduledTaskRunRequestedEvent changed => ApplyScheduledTaskRun(changed.EntityId, changed.ScheduledTaskRun),
        ScheduledTaskRunAdmissionRecordedEvent changed => ApplyScheduledTaskRun(changed.EntityId, changed.ScheduledTaskRun),
        ScheduledTaskRunStageRecordedEvent changed => ApplyScheduledTaskRun(changed.EntityId, changed.ScheduledTaskRun),
        ScheduledTaskRunStartedEvent changed => ApplyScheduledTaskRun(changed.EntityId, changed.ScheduledTaskRun),
        ScheduledTaskRunCompletedEvent changed => ApplyScheduledTaskRun(changed.EntityId, changed.ScheduledTaskRun),
        ScheduledTaskRunFailedEvent changed => ApplyScheduledTaskRun(changed.EntityId, changed.ScheduledTaskRun),
        ScheduledTaskRunUncertainRecordedEvent changed => ApplyScheduledTaskRun(changed.EntityId, changed.ScheduledTaskRun),
        _ => false
    };
    /// <summary>Accepts only an event for this identity with the next consecutive revision.</summary>
    private bool ApplyScheduledTaskRun(ScheduledTaskId identity, ScheduledTaskRun? change)
    {
        if (!identity.IsValid || change is null || change.Id != identity ||
            change.Revision != (ScheduledTaskRun?.Revision ?? 0) + 1) return false;
        ScheduledTaskRun = change;
        return true;
    }
}
