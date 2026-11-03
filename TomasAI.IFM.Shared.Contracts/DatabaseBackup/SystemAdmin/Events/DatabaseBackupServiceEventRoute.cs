using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
/// <summary>Routes host observations directly to the concern that handles that observation.</summary>
public static class DatabaseBackupServiceEventRoute
{
    public const string Backup = "DatabaseBackupEvent";
    public const string BackupSet = "DatabaseBackupSetEvent";
    public const string Policy = "DatabaseBackupPolicyEvent";
    public const string Recovery = "DatabaseRecoveryEvent";
    public const string Retention = "DatabaseBackupRetentionEvent";
    public const string Service = "DatabaseBackupServiceEvent";
    public static string For(string verb, DatabaseRecoveryOperationKind kind)
        => verb is "RunStatisticsCaptured" or "ServiceReconciliation" ? kind switch
        {
            DatabaseRecoveryOperationKind.Backup or DatabaseRecoveryOperationKind.Verification => Backup,
            DatabaseRecoveryOperationKind.Restore or DatabaseRecoveryOperationKind.RestoreDrill or DatabaseRecoveryOperationKind.Cutover => Recovery,
            DatabaseRecoveryOperationKind.Retention => Retention,
            DatabaseRecoveryOperationKind.Reconciliation => Policy,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "An observation owner is required.")
        } : For(verb);
    public static string For(string verb) => verb switch
    {
        "BackupAccepted" => Backup,
        "BackupRejected" => Backup,
        "BackupStarted" => Backup,
        "BackupProgress" => Backup,
        "BackupBoundaryEstablished" => Backup,
        "BackupVerificationCompleted" => Backup,
        "BackupError" => Backup,
        "BackupCompleted" => Backup,
        "BackupFailed" => Backup,
        "BackupCancelled" => Backup,
        "RestoreAccepted" => Recovery,
        "RestoreRejected" => Recovery,
        "RestoreStarted" => Recovery,
        "RestoreProgress" => Recovery,
        "RestoreValidationCompleted" => Recovery,
        "RestoreReadyForCutover" => Recovery,
        "RestoreDrillCompleted" => Recovery,
        "RestoreError" => Recovery,
        "RestoreCompleted" => Recovery,
        "RestoreFailed" => Recovery,
        "RestoreCancelled" => Recovery,
        "RunStatisticsCaptured" => Recovery,
        "PolicyApplied" => Policy,
        "PolicyRejected" => Policy,
        "RetentionPlanCreated" => Retention,
        "RetentionExecutionCompleted" => Retention,
        "RetentionExecutionFailed" => Retention,
        "BackupArtifactReplicaUpdated" => Backup,
        "ServiceReconciliation" => Service,
        "ServiceCapabilityChanged" => Service,
        _ => throw new ArgumentException("Unsupported database service event verb.", nameof(verb))
    };
}
