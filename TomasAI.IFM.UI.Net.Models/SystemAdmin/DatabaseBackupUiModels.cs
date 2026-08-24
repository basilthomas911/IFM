using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.UI.Net.Models.SystemAdmin;

/// <summary>Immutable protection-set state displayed by the database-backup UI.</summary>
/// <param name="Id">The protection-set identifier.</param>
/// <param name="Source">The backup source.</param>
/// <param name="Engines">The protected database engines.</param>
/// <param name="Enabled">Whether the protection set accepts backup requests.</param>
/// <param name="PolicyRevision">The optimistic policy revision.</param>
public sealed record DatabaseProtectionSetUiModel(
    string Id,
    BackupSource Source,
    IReadOnlyList<DatabaseEngine> Engines,
    bool Enabled,
    long PolicyRevision);

/// <summary>Immutable operation state displayed by the database-backup UI.</summary>
/// <param name="OperationId">The operation identifier.</param>
/// <param name="ProtectionSet">The protection-set identifier.</param>
/// <param name="Source">The backup source.</param>
/// <param name="Phase">The current recovery phase.</param>
/// <param name="Outcome">The current recovery outcome.</param>
/// <param name="ProgressPercent">The bounded progress percentage.</param>
/// <param name="SafeDiagnosticReference">The safe diagnostic reference.</param>
/// <param name="RequestedMode">The requested backup mode.</param>
/// <param name="ResolvedMode">The resolved backup mode.</param>
public sealed record DatabaseBackupOperationUiModel(
    Guid OperationId,
    string ProtectionSet,
    BackupSource Source,
    DatabaseRecoveryPhase Phase,
    DatabaseRecoveryOutcome Outcome,
    int ProgressPercent,
    string SafeDiagnosticReference,
    DatabaseBackupMode RequestedMode = DatabaseBackupMode.Full,
    DatabaseBackupMode ResolvedMode = DatabaseBackupMode.None);

/// <summary>Immutable restore-point summary displayed by the database-backup UI.</summary>
/// <param name="RestorePointId">The restore-point identifier.</param>
/// <param name="RecoveryPointUtc">The recovery point in UTC.</param>
/// <param name="VerificationLevel">The completed verification level.</param>
/// <param name="VerifiedUtc">The optional verification timestamp.</param>
/// <param name="RestoreTestedUtc">The optional restore-test timestamp.</param>
/// <param name="Eligible">Whether the restore point is eligible for use.</param>
public sealed record DatabaseRestorePointUiModel(
    string RestorePointId,
    DateTimeOffset RecoveryPointUtc,
    DatabaseVerificationLevel VerificationLevel,
    DateTimeOffset? VerifiedUtc,
    DateTimeOffset? RestoreTestedUtc,
    bool Eligible);

/// <summary>Represents one bounded query refresh of the database-backup dashboard.</summary>
/// <param name="Source">The selected backup source.</param>
/// <param name="ProtectionSets">The available protection sets.</param>
/// <param name="RecentOperations">The recent backup operations.</param>
/// <param name="LatestVerified">The latest verified restore point.</param>
/// <param name="LatestRestoreTested">The latest restore-tested point.</param>
public sealed record DatabaseBackupDashboardUiModel(
    BackupSource Source,
    IReadOnlyList<DatabaseProtectionSetUiModel> ProtectionSets,
    IReadOnlyList<DatabaseBackupOperationUiModel> RecentOperations,
    DatabaseRestorePointUiModel? LatestVerified,
    DatabaseRestorePointUiModel? LatestRestoreTested);

/// <summary>Identifies a backup command accepted for asynchronous processing.</summary>
/// <param name="OperationId">The accepted operation identifier.</param>
public sealed record DatabaseBackupAcceptedUiModel(Guid OperationId);

/// <summary>Identifies a backup-domain notification that requires a bounded dashboard refresh.</summary>
/// <param name="EntityId">The changed backup entity identifier.</param>
public sealed record DatabaseBackupNotificationUiModel(Guid EntityId);
