using MessagePack;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;

[MessagePackObject]
public sealed record DatabaseProtectionSetReadModel
{
    [Key(0)] public DatabaseProtectionSetId ProtectionSetId { get; init; }
    [Key(1)] public BackupSource Source { get; init; }
    [Key(2)] public DatabaseEngine[] Engines { get; init; } = [];
    [Key(3)] public bool Enabled { get; init; }
    [Key(4)] public long PolicyRevision { get; init; }
}

[MessagePackObject]
public sealed record DatabaseBackupOperationReadModel
{
    [Key(0)] public DatabaseRecoveryOperationId OperationId { get; init; }
    [Key(1)] public DatabaseBackupSetId? BackupSetId { get; init; }
    [Key(2)] public DatabaseProtectionSetId ProtectionSetId { get; init; }
    [Key(3)] public BackupSource Source { get; init; }
    [Key(4)] public DatabaseRecoveryOperationKind Kind { get; init; }
    [Key(5)] public DatabaseRecoveryPhase Phase { get; init; }
    [Key(6)] public DatabaseRecoveryOutcome Outcome { get; init; }
    [Key(7)] public int ProgressPercent { get; init; }
    [Key(8)] public long StateRevision { get; init; }
    [Key(9)] public DateTimeOffset CreatedUtc { get; init; }
    [Key(10)] public DateTimeOffset? CompletedUtc { get; init; }
    [Key(11)] public string SafeDiagnosticReference { get; init; } = string.Empty;
    [Key(12)] public DatabaseBackupLineage? BackupLineage { get; init; }
}

[MessagePackObject]
public sealed record DatabaseBackupSetReadModel
{
    [Key(0)] public DatabaseBackupSetId BackupSetId { get; init; }
    [Key(1)] public BackupSource Source { get; init; }
    [Key(2)] public DatabaseRecoveryOperationId[] OperationIds { get; init; } = [];
    [Key(3)] public int RequiredOperationCount { get; init; }
    [Key(4)] public int CompletedOperationCount { get; init; }
    [Key(5)] public bool Complete { get; init; }
    [Key(6)] public long Revision { get; init; }
}

[MessagePackObject]
public sealed record DatabaseRestorePointReadModel
{
    [Key(0)] public DatabaseRestorePointId RestorePointId { get; init; }
    [Key(1)] public DatabaseBackupSetId? BackupSetId { get; init; }
    [Key(2)] public DatabaseProtectionSetId ProtectionSetId { get; init; }
    [Key(3)] public BackupSource Source { get; init; }
    [Key(4)] public DateTimeOffset RecoveryPointUtc { get; init; }
    [Key(5)] public DatabaseVerificationLevel VerificationLevel { get; init; }
    [Key(6)] public DateTimeOffset? VerifiedUtc { get; init; }
    [Key(7)] public DateTimeOffset? RestoreTestedUtc { get; init; }
    [Key(8)] public bool Eligible { get; init; }
    [Key(9)] public bool LegalHold { get; init; }
    [Key(10)] public long ManifestRevision { get; init; }
    [Key(11)] public DatabaseBackupLineage? BackupLineage { get; init; }
}

[MessagePackObject]
public sealed record DatabaseRestoreOperationReadModel
{
    [Key(0)] public DatabaseBackupOperationReadModel Operation { get; init; } = new();
    [Key(1)] public DatabaseRestorePointId RestorePointId { get; init; }
    [Key(2)] public DatabaseRestoreClass RestoreClass { get; init; }
    [Key(3)] public string FreshTargetProfile { get; init; } = string.Empty;
    [Key(4)] public long ValidationRevision { get; init; }
    [Key(5)] public DatabaseCutoverState CutoverState { get; init; }
}

[MessagePackObject]
public sealed record DatabaseBackupPolicyReadModel
{
    [Key(0)] public DatabaseBackupPolicyId PolicyId { get; init; }
    [Key(1)] public string EnvironmentIdentity { get; init; } = string.Empty;
    [Key(2)] public long Revision { get; init; }
    [Key(3)] public DatabaseBackupPolicyDefinition Definition { get; init; } = new([], [], new(TimeSpan.Zero, TimeSpan.Zero), new(0, 0, 0), new([], TimeSpan.Zero));
    [Key(4)] public bool Enforced { get; init; }
}

[MessagePackObject]
public sealed record DatabaseBackupHealthReadModel
{
    [Key(0)] public BackupSource Source { get; init; }
    [Key(1)] public DatabaseBackupHostId HostId { get; init; }
    [Key(2)] public DatabaseServiceCapabilityState CapabilityState { get; init; }
    [Key(3)] public bool Ready { get; init; }
    [Key(4)] public long LastServiceSequence { get; init; }
    [Key(5)] public DateTimeOffset ObservedUtc { get; init; }
    [Key(6)] public string SafeDiagnosticReference { get; init; } = string.Empty;
}

[MessagePackObject]
public sealed record DatabaseRetentionReadModel
{
    [Key(0)] public DatabaseRetentionPlanId PlanId { get; init; }
    [Key(1)] public BackupSource Source { get; init; }
    [Key(2)] public long PlanRevision { get; init; }
    [Key(3)] public DateTimeOffset EvaluationBoundaryUtc { get; init; }
    [Key(4)] public DatabaseRestorePointId[] Retain { get; init; } = [];
    [Key(5)] public DatabaseRestorePointId[] Delete { get; init; } = [];
    [Key(6)] public bool Approved { get; init; }
    [Key(7)] public DatabaseRecoveryOutcome Outcome { get; init; }
}

[MessagePackObject]
public sealed record DatabaseRecoveryRunStatsReadModel
{
    [Key(0)] public DatabaseRecoveryOperationId OperationId { get; init; }
    [Key(1)] public BackupSource Source { get; init; }
    [Key(2)] public long StatisticsRevision { get; init; }
    [Key(3)] public DatabaseRecoveryRunStatistics[] Statistics { get; init; } = [];
}
