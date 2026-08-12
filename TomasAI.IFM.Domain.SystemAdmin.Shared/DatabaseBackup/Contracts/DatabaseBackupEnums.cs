namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

public enum BackupSource { None = 0, LocalWorkstation = 1, AwsCloud = 2 }
public enum DatabaseRecoveryOperationKind { None = 0, Backup = 1, Verification = 2, Restore = 3, RestoreDrill = 4, Cutover = 5, Reconciliation = 6, Retention = 7 }
public enum DatabaseEngine { None = 0, PostgreSql = 1, ScyllaDb = 2 }
public enum DatabaseRecoveryPhase
{
    None = 0, Requested = 1, Authorized = 2, Admitted = 3, Preflight = 4, Started = 5,
    EstablishingBoundary = 6, Capturing = 7, Transferring = 8, Verifying = 9,
    Validating = 10, ReadyForCutover = 11, CuttingOver = 12, Reconciling = 13,
    Completed = 14, Failed = 15, Cancelled = 16, Rejected = 17
}
public enum DatabaseRecoveryOutcome { None = 0, Succeeded = 1, Failed = 2, Cancelled = 3, Rejected = 4, Degraded = 5 }
public enum DatabaseRequestOrigin { None = 0, UI = 1, Console = 2, ScheduledTask = 3, Reconciliation = 4 }
public enum DatabaseArtifactReplicaState { None = 0, Planned = 1, Staging = 2, Transferring = 3, Durable = 4, Verified = 5, Published = 6, Failed = 7, Deleted = 8 }
public enum DatabaseVerificationLevel { None = 0, Checksum = 1, Native = 2, IsolatedRestore = 3, ApplicationValidation = 4 }
public enum DatabaseErrorClassification { None = 0, Retryable = 1, OperatorActionable = 2, Terminal = 3 }
public enum DatabaseRestoreClass { None = 0, Drill = 1, ProductionRecovery = 2 }
public enum DatabaseCutoverState { None = 0, NotRequested = 1, AwaitingApproval = 2, Approved = 3, InProgress = 4, Completed = 5, Rejected = 6 }
public enum DatabaseServiceCapabilityState { None = 0, Unknown = 1, Ready = 2, Degraded = 3, Unavailable = 4, Disabled = 5 }
public enum DatabaseConsistencyMode { None = 0, EngineConsistent = 1, CoordinatedProtectionSet = 2 }

public static class DatabaseBackupEnumValidation
{
    public static void RequireConcrete(BackupSource source, string parameterName = "source")
    {
        if (source is not (BackupSource.LocalWorkstation or BackupSource.AwsCloud))
            throw new ArgumentOutOfRangeException(parameterName, source, "A concrete, supported backup source is required.");
    }

    public static void RequireDefined<TEnum>(TEnum value, string parameterName) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value) || Convert.ToInt32(value) == 0)
            throw new ArgumentOutOfRangeException(parameterName, value, "A defined non-zero value is required.");
    }

    public static void RequireOptionalDefined<TEnum>(TEnum value, string parameterName) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(parameterName, value, "A defined value is required.");
    }
}
