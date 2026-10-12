using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;

/// <summary>Shared non-serialized fields of the direct DatabaseBackup queries.</summary>
public interface IDatabaseBackupQuery : IQuery
{
    string Verb { get; }
    new ActorSubject Subject { get; init; }
    new DatabaseRecoveryOperationId EntityId { get; init; }
    DatabaseRequestEnvelope Request { get; init; }
    BackupSource Source { get; init; }
    DatabaseRecoveryOperationId? OperationId { get; init; }
    DatabaseBackupSetId? BackupSetId { get; init; }
    DatabaseRestorePointId? RestorePointId { get; init; }
    DatabaseBackupPolicyId? PolicyId { get; init; }
    DatabaseProtectionSetId? ProtectionSetId { get; init; }
    DatabaseRetentionPlanId? RetentionPlanId { get; init; }
    int PageSize { get; init; }
    string ContinuationIdentity { get; init; }
    DateTimeOffset? FromUtc { get; init; }
    DateTimeOffset? ToUtc { get; init; }
    void Validate();
}
