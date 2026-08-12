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
        new("database_recovery_operation_v1", SystemAdminSchemaSql.CreateRecoveryOperation, "DROP TABLE IF EXISTS system_admin.database_recovery_operation_v1;"),
        new("database_recovery_phase_v1", SystemAdminSchemaSql.CreateRecoveryPhase, "DROP TABLE IF EXISTS system_admin.database_recovery_phase_v1;"),
        new("database_recovery_run_stats_v1", SystemAdminSchemaSql.CreateRecoveryRunStats, "DROP TABLE IF EXISTS system_admin.database_recovery_run_stats_v1;"),
        new("database_restore_point_v1", SystemAdminSchemaSql.CreateRestorePoint, "DROP TABLE IF EXISTS system_admin.database_restore_point_v1;"),
        new("database_artifact_replica_v1", SystemAdminSchemaSql.CreateArtifactReplica, "DROP TABLE IF EXISTS system_admin.database_artifact_replica_v1;"),
        new("database_recovery_error_v1", SystemAdminSchemaSql.CreateRecoveryError, "DROP TABLE IF EXISTS system_admin.database_recovery_error_v1;"),
        new("database_backup_policy_v1", SystemAdminSchemaSql.CreateBackupPolicy, "DROP TABLE IF EXISTS system_admin.database_backup_policy_v1;"),
        new("database_backup_service_health_v1", SystemAdminSchemaSql.CreateServiceHealth, "DROP TABLE IF EXISTS system_admin.database_backup_service_health_v1;"),
        new("database_retention_state_v1", SystemAdminSchemaSql.CreateRetentionState, "DROP TABLE IF EXISTS system_admin.database_retention_state_v1;"),
        new("database_backup_projection_checkpoint_v1", SystemAdminSchemaSql.CreateProjectionCheckpoint, "DROP TABLE IF EXISTS system_admin.database_backup_projection_checkpoint_v1;"),
        new("database_backup_projection_receipt_v1", SystemAdminSchemaSql.CreateProjectionReceipt, "DROP TABLE IF EXISTS system_admin.database_backup_projection_receipt_v1;")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
