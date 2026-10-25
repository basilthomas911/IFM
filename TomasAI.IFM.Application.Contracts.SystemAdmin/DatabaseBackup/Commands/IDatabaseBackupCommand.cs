using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;

/// <summary>Shared non-serialized fields of the direct DatabaseBackup commands.</summary>
public interface IDatabaseBackupCommand : ICommand<DatabaseRecoveryOperationId>
{
    string Verb { get; }
    DatabaseRequestEnvelope Request { get; init; }
    BackupSource Source { get; init; }
    DatabaseProtectionSetId? ProtectionSetId { get; init; }
    DatabaseConsistencyMode ConsistencyMode { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    DatabaseLogicalDestination[] RequiredDestinations { get; init; }
    long ExpectedPolicyRevision { get; init; }
    long ExpectedStateRevision { get; init; }
    string SafeReason { get; init; }
    DatabaseRestorePointId? RestorePointId { get; init; }
    DatabaseFreshTargetDescriptor? FreshTarget { get; init; }
    DatabaseRestoreClass RestoreClass { get; init; }
    long ExpectedManifestRevision { get; init; }
    string ApprovalIdentity { get; init; }
    string ApprovalReference { get; init; }
    long ValidationRevision { get; init; }
    string DisposableTargetProfile { get; init; }
    string ValidationProfile { get; init; }
    DatabaseBackupPolicyId? PolicyId { get; init; }
    DatabaseBackupPolicyDefinition? Policy { get; init; }
    DatabaseBackupSetId? BackupSetId { get; init; }
    string LegalHoldReference { get; init; }
    long ExpectedLegalHoldRevision { get; init; }
    DateTimeOffset EvaluationBoundaryUtc { get; init; }
    DatabaseRetentionPlanId? RetentionPlanId { get; init; }
    long RetentionPlanRevision { get; init; }
    DatabaseBackupMode RequestedBackupMode { get; init; }
    void Validate();
}
