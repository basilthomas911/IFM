using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
/// <summary>Concern ownership of operator intents and operation-scoped service receipts.</summary>
public static class DatabaseBackupCommandRoute
{
    // Retained as the historical backup route and persisted stream prefix.
    public const string Actor = "DatabaseBackupCommand";
    public const string Backup = "DatabaseBackupCommand";
    public const string Recovery = "DatabaseRecoveryCommand";
    public const string Policy = "DatabaseBackupPolicyCommand";
    public const string Retention = "DatabaseBackupRetentionCommand";
    public const string BackupSet = "DatabaseBackupSetCommand";
    public const string Service = "DatabaseBackupServiceCommand";
    public static string For(IDatabaseBackupCommand command) => command switch
    {
        CancelDatabaseBackupCommand => Backup,
        RequestDatabaseBackupCommand => Backup,
        ApproveDatabaseCutoverCommand => Recovery,
        ApproveDatabaseRestoreCommand => Recovery,
        CancelDatabaseRestoreCommand => Recovery,
        RequestDatabaseRestoreCommand => Recovery,
        RequestDatabaseRestoreDrillCommand => Recovery,
        UpdateDatabaseBackupPolicyCommand => Policy,
        ExecuteBackupRetentionPlanCommand => Retention,
        RequestBackupRetentionEvaluationCommand => Retention,
        PlaceBackupLegalHoldCommand => BackupSet,
        ReleaseBackupLegalHoldCommand => BackupSet,
        _ => throw new ArgumentException("Unsupported database operation command.", nameof(command))
    };
    public static string For(DatabaseBackupInternalCommand command) => command switch
    {
        RecordDatabaseBackupPolicyStatusCommand => Policy,
        RecordDatabaseBackupServiceCapabilityCommand => Service,
        _ => For(command.Source.OperationKind)
    };
    public static string For(DatabaseRecoveryOperationKind kind) => kind switch
    {
        DatabaseRecoveryOperationKind.Backup or DatabaseRecoveryOperationKind.Verification => Backup,
        DatabaseRecoveryOperationKind.Restore or DatabaseRecoveryOperationKind.RestoreDrill or DatabaseRecoveryOperationKind.Cutover => Recovery,
        DatabaseRecoveryOperationKind.Retention => Retention,
        DatabaseRecoveryOperationKind.Reconciliation => Policy,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "An operation owner is required.")
    };
}
