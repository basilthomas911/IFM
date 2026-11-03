namespace TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
public static class DatabaseBackupQueryRoute
{
    public const string Actor = "DatabaseBackupQuery";
    public const string Backup = "DatabaseBackupQuery";
    public const string BackupSet = "DatabaseBackupSetQuery";
    public const string Lifecycle = "DatabaseBackupLifecycleQuery";
    public const string Policy = "DatabaseBackupPolicyQuery";
    public const string Recovery = "DatabaseRecoveryQuery";
    public const string Retention = "DatabaseBackupRetentionQuery";
    public const string Service = "DatabaseBackupServiceQuery";
    public static string For(IDatabaseBackupQuery query) => query switch
    {
        GetDatabaseBackupOperationQuery => Backup,
        GetLatestVerifiedDatabaseBackupQuery => Backup,
        ListDatabaseBackupOperationsQuery => Backup,
        GetDatabaseRecoveryObjectiveComplianceQuery => Recovery,
        GetDatabaseRecoveryRunStatsQuery => Recovery,
        GetDatabaseRestoreOperationQuery => Recovery,
        GetDatabaseRestorePointQuery => Recovery,
        GetLatestRestoreTestedDatabaseBackupQuery => Recovery,
        ListDatabaseRestoreDrillsQuery => Recovery,
        ListDatabaseRestorePointsQuery => Recovery,
        GetDatabaseBackupPolicyQuery => Policy,
        GetDatabaseBackupSetupQuery => Policy,
        GetDatabaseRetentionForecastQuery => Retention,
        GetDatabaseBackupSetQuery => BackupSet,
        GetDatabaseBackupServiceHealthQuery => Service,
        GetDatabaseProtectionSetsQuery => Service,
        GetDatabaseBackupLogQuery => Lifecycle,
        _ => throw new ArgumentException("Unsupported database operation query.", nameof(query))
    };
}
