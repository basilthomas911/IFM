using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;

public sealed class ParameterAssignmentCommandState : BaseEventSourceActorState<ParameterAssignmentCommandState>, IEventSourceActorState<ParameterAssignmentCommandState>
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Actor reducers and command models share this mutable state dictionary; preserve the published state contract during the persistence refactor.")]
    public Dictionary<Guid, ParameterAuditEntry> Audit { get; } = [];

    public override ActorThreadId Id { get; set; } = default!;
    public ParameterAssignmentRevision? Assignment { get; private set; }
    public long Revision => Assignment?.Revision ?? 0;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Actor reducers and command models share this mutable state dictionary; preserve the published state contract during the persistence refactor.")]
    public Dictionary<Guid, string> Operations { get; } = [];
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not ParameterAssignmentChangedEvent fact || fact.Revision != Revision + 1) return false;
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
