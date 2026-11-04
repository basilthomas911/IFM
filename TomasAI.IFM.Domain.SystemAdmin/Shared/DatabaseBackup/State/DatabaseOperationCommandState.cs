using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
/// <summary>Common event application mechanics for one concern-owned operation.</summary>
public abstract class DatabaseOperationCommandState<TState> : BaseEventSourceActorState<TState>, IEventSourceActorState<TState>
    where TState : DatabaseOperationCommandState<TState>
{
    readonly Dictionary<Guid,string> _processedServiceEvents = [];
    public override ActorThreadId Id { get; set; } = default!;
    public DatabaseBackupOperationState Operation { get; private set; } = new();
    internal DatabaseOperationDecisionState OperationDecision => new(Operation, _processedServiceEvents);
    internal bool Update(IReadOnlyList<DatabaseBackupEventContract> events, ICommand command)
    {
        if (events.Any(e => e.CommandId != command.CommandId)) return false;
        foreach (var e in events) if (!Update(e, command)) return false;
        return true;
    }
    internal long LegacyStreamVersion { get; set; }

    internal DatabaseOperationStateSnapshottedEvent CaptureSnapshot(ICommand command) => CaptureConcern(new()
    {
        Subject = new(ActorType.Event, "DatabaseBackupEvent", "StateSnapshotted", command.Subject.EntityId),
        Id = Guid.NewGuid(),
        CommandId = command.CommandId,
        EventSource = command.EventSource,
        ReceivedOn = DateTime.UtcNow,
        Concern = typeof(TState).Name,
        LegacyStreamVersion = LegacyStreamVersion,
        Operation = Operation,
        ProcessedServiceEvents = new Dictionary<Guid, string>(_processedServiceEvents)
    });

    internal bool IsCompatibleSnapshot(IEvent fact) =>
        fact is DatabaseOperationStateSnapshottedEvent snapshot &&
        snapshot.Concern == typeof(TState).Name && snapshot.LegacyStreamVersion == LegacyStreamVersion &&
        snapshot.Operation is { } operation && snapshot.ProcessedServiceEvents is not null &&
        (Operation.Exists ? Operation.OperationId == operation.OperationId : true) &&
        HasConcernSnapshot(snapshot);

    protected virtual DatabaseOperationStateSnapshottedEvent CaptureConcern(DatabaseOperationStateSnapshottedEvent snapshot) => snapshot;
    protected virtual bool HasConcernSnapshot(DatabaseOperationStateSnapshottedEvent snapshot) => true;
    protected virtual bool RestoreConcern(DatabaseOperationStateSnapshottedEvent snapshot) => true;

    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is DatabaseOperationStateSnapshottedEvent snapshot)
        {
            if (!IsCompatibleSnapshot(snapshot) || !RestoreConcern(snapshot))
                return false;
            Operation = snapshot.Operation!;
            _processedServiceEvents.Clear();
            foreach (var entry in snapshot.ProcessedServiceEvents!)
                _processedServiceEvents.Add(entry.Key, entry.Value);
            return true;
        }

        if (domainEvent is not DatabaseBackupEventContract e || e.GetType().Namespace?.EndsWith(".Events.Domain", StringComparison.Ordinal) != true) return false;
        Operation = Operation.Apply(e);
        ApplyConcern(e);
        if (e.Source.ProducingHostId is not null)
            _processedServiceEvents[e.Source.SourceEventId] = DatabaseOperationChanges.Fingerprint(DatabaseBackupEventFactory.Describe(e));
        return true;
    }
    protected abstract void ApplyConcern(DatabaseBackupEventContract source);
}
