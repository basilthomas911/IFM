using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SystemAdminDb.Schema;

public sealed class SystemAdminSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<SystemAdminSchemaDb>(connectionSettings[SystemAdminDbContext.SystemAdminDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("system_admin", SystemAdminSchemaSql.CreateSchema, "DROP SCHEMA IF EXISTS system_admin;"),
        new("database_recovery_operation", SystemAdminSchemaSql.CreateRecoveryOperation, "DROP TABLE IF EXISTS system_admin.database_recovery_operation;"),
        new("database_recovery_phase", SystemAdminSchemaSql.CreateRecoveryPhase, "DROP TABLE IF EXISTS system_admin.database_recovery_phase;"),
        new("database_recovery_run_stats", SystemAdminSchemaSql.CreateRecoveryRunStats, "DROP TABLE IF EXISTS system_admin.database_recovery_run_stats;"),
        new("database_restore_point", SystemAdminSchemaSql.CreateRestorePoint, "DROP TABLE IF EXISTS system_admin.database_restore_point;"),
        new("database_artifact_replica", SystemAdminSchemaSql.CreateArtifactReplica, "DROP TABLE IF EXISTS system_admin.database_artifact_replica;"),
        new("database_recovery_error", SystemAdminSchemaSql.CreateRecoveryError, "DROP TABLE IF EXISTS system_admin.database_recovery_error;"),
        new("database_backup_policy", SystemAdminSchemaSql.CreateBackupPolicy, "DROP TABLE IF EXISTS system_admin.database_backup_policy;"),
        new("database_backup_service_health", SystemAdminSchemaSql.CreateServiceHealth, "DROP TABLE IF EXISTS system_admin.database_backup_service_health;"),
        new("database_retention_state", SystemAdminSchemaSql.CreateRetentionState, "DROP TABLE IF EXISTS system_admin.database_retention_state;"),
        new("database_backup_projection_checkpoint", SystemAdminSchemaSql.CreateProjectionCheckpoint, "DROP TABLE IF EXISTS system_admin.database_backup_projection_checkpoint;"),
        new("database_backup_projection_receipt", SystemAdminSchemaSql.CreateProjectionReceipt, "DROP TABLE IF EXISTS system_admin.database_backup_projection_receipt;")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
