using System.Text.Json;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.State;

public sealed class ParameterSetCommandState : BaseEventSourceActorState<ParameterSetCommandState>, IEventSourceActorState<ParameterSetCommandState>
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Actor reducers and command models share this mutable state dictionary; preserve the published state contract during the persistence refactor.")]
    public Dictionary<Guid, ParameterAuditEntry> Audit { get; } = [];

    public override ActorThreadId Id { get; set; } = default!;
    public long CatalogRevision { get; private set; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Actor reducers and command models share this mutable state dictionary; preserve the published state contract during the persistence refactor.")]
    public Dictionary<int, ParameterSetVersion> Versions { get; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Actor reducers and command models share this mutable state dictionary; preserve the published state contract during the persistence refactor.")]
    public Dictionary<Guid, string> Operations { get; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Actor reducers and command models share this mutable state dictionary; preserve the published state contract during the persistence refactor.")]
    public Dictionary<Guid, ParameterOperationReceipt> Receipts { get; } = [];
    /// <summary>Requires every accepted source fact to participate in ordered replay.</summary>
    protected override bool ApplyReplayEvent(IEvent domainEvent)
    {
        var applied = base.ApplyReplayEvent(domainEvent);
        if (domainEvent is IParameterSetFact && !applied) throw new InvalidDataException("PARAM.SET_REPLAY_GAP");
        return applied;
    }

    internal ParameterSetStateSnapshottedEvent CaptureSnapshot(ICommand command) => new()
    {
        Subject = new(ActorType.Event, "ParameterSetEvent", "StateSnapshotted", command.Subject.EntityId),
        Id = Guid.NewGuid(),
        CommandId = command.CommandId,
        EventSource = command.EventSource,
        ReceivedOn = DateTime.UtcNow,
        OwnerId = Versions.Values.First().Reference.SetId,
        Operations = new Dictionary<Guid, string>(Operations),
        Audit = new Dictionary<Guid, ParameterAuditEntry>(Audit),
        CatalogRevision = CatalogRevision,
        Versions = new Dictionary<int, ParameterSetVersion>(Versions),
        Receipts = new Dictionary<Guid, ParameterOperationReceipt>(Receipts)
    };

    internal static bool IsCompleteSnapshot(ParameterSetStateSnapshottedEvent snapshot) =>
        snapshot.OwnerId != Guid.Empty && snapshot.Operations is not null && snapshot.Audit is not null &&
        snapshot.CatalogRevision > 0 && snapshot.Versions is { Count: > 0 } && snapshot.Receipts is not null;

    private bool RestoreSnapshot(ParameterSetStateSnapshottedEvent snapshot)
    {
        if (!IsCompleteSnapshot(snapshot))
            return false;
        if (!string.IsNullOrEmpty(Id.EntityId) && (!Guid.TryParse(Id.EntityId, out var owner) || owner != snapshot.OwnerId))
            throw new InvalidDataException("PARAM.SET_OWNER_MISMATCH");
        Operations.Clear();
        Audit.Clear();
        Versions.Clear();
        Receipts.Clear();
        foreach (var entry in snapshot.Operations!)
            Operations.Add(entry.Key, entry.Value);
        foreach (var entry in snapshot.Audit!)
            Audit.Add(entry.Key, entry.Value);
        foreach (var entry in snapshot.Versions!)
            Versions.Add(entry.Key, entry.Value);
        foreach (var entry in snapshot.Receipts!)
            Receipts.Add(entry.Key, entry.Value);
        CatalogRevision = snapshot.CatalogRevision;
        return true;
    }

    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is ParameterSetStateSnapshottedEvent snapshot)
            return RestoreSnapshot(snapshot);
        if (domainEvent is not IParameterSetFact fact || fact.Revision != CatalogRevision + 1) return false;
        if (!string.IsNullOrEmpty(Id.EntityId) && (!Guid.TryParse(Id.EntityId, out var owner) || owner != fact.EntityId.SetId))
            throw new InvalidDataException("PARAM.SET_OWNER_MISMATCH");
        var version = fact.ReadVersion();
        if (version.Reference.SetId != fact.EntityId.SetId) throw new InvalidDataException("PARAM.SET_OWNER_MISMATCH");
        Versions[version.Reference.Version] = version;
        // Set metadata is shared across versions; immutable payloads are never rewritten.
        foreach (var key in Versions.Keys.ToArray())
            Versions[key] = Versions[key] with { Name = version.Name, Description = version.Description, CatalogRevision = fact.Revision };
        Operations[fact.CommandId] = fact.RequestHash;
        Receipts[fact.CommandId] = new(fact.CommandId, fact.RequestHash, fact.Revision, version.Reference);
        if (fact.ReadAudit() is {} entry)
        {
            if(entry.OperationId!=fact.CommandId||entry.Revision!=fact.Revision) throw new InvalidDataException("Audit identity mismatch");
            Audit[fact.CommandId]=entry;
        }
        CatalogRevision = fact.Revision;
        return true;
    }
}
