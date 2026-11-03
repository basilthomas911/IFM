using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Service.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.BackupSet.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Backup.Command.State;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Model;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Legacy;
/// <summary>Owns one existing operation stream; concern records are operation-local facts, not separate global aggregates. Business decisions live in the concrete concern handlers.</summary>
public sealed class DatabaseBackupReplayState : BaseEventSourceActorState<DatabaseBackupReplayState>, IEventSourceActorState<DatabaseBackupReplayState>
{
    readonly Dictionary<Guid, string> _processedServiceEvents = [];
    public override ActorThreadId Id { get; set; } = default!;
    public DatabaseBackupOperationState Operation { get; private set; } = new();
    public DatabaseRestoreOperationState Restore { get; private set; } = new();
    public DatabaseBackupSetState BackupSet { get; private set; } = new();
    public DatabaseBackupPolicyState Policy { get; private set; } = new();
    public DatabaseBackupServiceState Service { get; private set; } = new();
    public DatabaseRetentionState Retention { get; private set; } = new();

    internal DatabaseBackupDecisionState BackupDecision => new(Operation);
    internal DatabaseRecoveryDecisionState RecoveryDecision => new(Operation, Restore);
    internal DatabasePolicyDecisionState PolicyDecision => new(Operation, Policy);
    internal DatabaseBackupSetDecisionState BackupSetDecision => new(Operation, BackupSet);
    internal DatabaseRetentionDecisionState RetentionDecision => new(Operation, Retention);
    internal DatabaseOperationDecisionState OperationDecision => new(Operation, _processedServiceEvents);
    internal DatabaseServiceReconciliationDecisionState ReconciliationDecision => new(Operation, Restore, Retention, _processedServiceEvents);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "S3267", Justification = "Apply accepted facts in order and stop at the first rejection; state mutation must remain explicit rather than becoming a filtering predicate.")]
    /// <summary>Applies an already validated ordered lifecycle event batch.</summary>
    /// <param name = "lifecycleEvents">The source events created from accepted business changes.</param>
    /// <param name = "command">The originating command.</param>
    /// <returns>True when every source event is applied, including an idempotent empty batch.</returns>
    internal bool Update(IReadOnlyList<DatabaseBackupEventContract> lifecycleEvents, ICommand command)
    {
        if (lifecycleEvents.Any(sourceEvent => sourceEvent.CommandId != command.CommandId))
            return false;
        foreach (var sourceEvent in lifecycleEvents)
            if (!Update(sourceEvent, command))
                return false;
        return true;
    }

    /// <summary>Replays the shared operation stream through concern-owned state application.</summary>
    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not DatabaseBackupEventContract e || e.GetType().Namespace?.EndsWith(".Events.Domain", StringComparison.Ordinal) != true)
            return false;
        Operation = Operation.Apply(e);
        Restore = Restore.Apply(e);
        BackupSet = BackupSet.Apply(e);
        Policy = Policy.Apply(e);
        Service = Service.Apply(e);
        Retention = Retention.Apply(e);
        if (e.Source.ProducingHostId is not null)
            _processedServiceEvents[e.Source.SourceEventId] = DatabaseOperationChanges.Fingerprint(DatabaseBackupEventFactory.Describe(e));
        return true;
    }
}
