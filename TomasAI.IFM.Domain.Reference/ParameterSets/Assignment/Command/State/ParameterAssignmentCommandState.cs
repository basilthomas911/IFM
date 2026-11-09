using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.State;

public sealed class ParameterAssignmentCommandState : BaseEventSourceActorState<ParameterAssignmentCommandState>, IEventSourceActorState<ParameterAssignmentCommandState>
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Actor reducers and command models share this mutable state dictionary; preserve the published state contract during the persistence refactor.")]
    public Dictionary<Guid, ParameterAuditEntry> Audit { get; } = [];

    public override ActorThreadId Id { get; set; } = default!;
    public ParameterAssignmentRevision? Assignment { get; private set; }
    public long Revision => Assignment?.Revision ?? 0;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Actor reducers and command models share this mutable state dictionary; preserve the published state contract during the persistence refactor.")]
    public Dictionary<Guid, string> Operations { get; } = [];
    /// <summary>Requires every accepted source fact to participate in ordered replay.</summary>
    protected override bool ApplyReplayEvent(IEvent domainEvent)
    {
        var applied = base.ApplyReplayEvent(domainEvent);
        if (domainEvent is ParameterAssignmentChangedEvent && !applied) throw new InvalidDataException("PARAM.ASSIGNMENT_REPLAY_GAP");
        return applied;
    }

    internal ParameterAssignmentStateSnapshottedEvent CaptureSnapshot(ICommand command) => new()
    {
        Subject = new(ActorType.Event, "ParameterAssignmentEvent", "StateSnapshotted", command.Subject.EntityId),
        Id = Guid.NewGuid(),
        CommandId = command.CommandId,
        EventSource = command.EventSource,

        OwnerId = Assignment!.AssignmentId,
        Operations = new Dictionary<Guid, string>(Operations),
        Audit = new Dictionary<Guid, ParameterAuditEntry>(Audit),
        Assignment = Assignment
    };

    internal static bool IsCompleteSnapshot(ParameterAssignmentStateSnapshottedEvent snapshot) =>
        snapshot.OwnerId != Guid.Empty && snapshot.Operations is not null && snapshot.Audit is not null &&
        snapshot.Assignment is { Revision: > 0 } assignment && assignment.AssignmentId == snapshot.OwnerId;

    private bool RestoreSnapshot(ParameterAssignmentStateSnapshottedEvent snapshot)
    {
        if (!IsCompleteSnapshot(snapshot))
            return false;
        if (!string.IsNullOrEmpty(Id.EntityId) && (!Guid.TryParse(Id.EntityId, out var owner) || owner != snapshot.OwnerId))
            throw new InvalidDataException("PARAM.ASSIGNMENT_OWNER_MISMATCH");
        Operations.Clear();
        Audit.Clear();
        foreach (var entry in snapshot.Operations!)
            Operations.Add(entry.Key, entry.Value);
        foreach (var entry in snapshot.Audit!)
            Audit.Add(entry.Key, entry.Value);
        Assignment = snapshot.Assignment;
        return true;
    }

    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is ParameterAssignmentStateSnapshottedEvent snapshot)
            return RestoreSnapshot(snapshot);
        if (domainEvent is not ParameterAssignmentChangedEvent fact || fact.Revision != Revision + 1) return false;
        if (!string.IsNullOrEmpty(Id.EntityId) && (!Guid.TryParse(Id.EntityId, out var owner) || owner != fact.EntityId.AssignmentId))
            throw new InvalidDataException("PARAM.ASSIGNMENT_OWNER_MISMATCH");
        var value = fact.ReadAssignment();
        if (value.Revision != fact.Revision || value.AssignmentId != fact.EntityId.AssignmentId) throw new InvalidDataException("Assignment identity mismatch.");
        if (fact.ReadAudit() is {} entry)
        {
            if (entry.OperationId != fact.CommandId || entry.Revision != fact.Revision) throw new InvalidDataException("Audit identity mismatch.");
            Audit[fact.CommandId] = entry;
        }
        Assignment = value; Operations[fact.CommandId] = fact.RequestHash; return true;
    }
}
