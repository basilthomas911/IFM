using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.SystemAdminDb.Schema;
/// <summary>Creates all PostgreSQL SystemAdmin tables and indexes in dependency order.</summary>
/// <param name = "connectionSettings">The named PostgreSQL connection settings.</param>
/// <param name = "logger">The schema logger.</param>
public sealed class SystemAdminSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger) : SchemaDbContext<SystemAdminSchemaDb>(connectionSettings[SystemAdminDbContext.SystemAdminDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects = [new("system_admin", SystemAdminSchemaSql.CreateSchema, SystemAdminSchemaSql.DropSchema), new("database_recovery_operation", SystemAdminSchemaSql.CreateRecoveryOperation, SystemAdminSchemaSql.DropRecoveryOperation), new("database_recovery_phase", SystemAdminSchemaSql.CreateRecoveryPhase, SystemAdminSchemaSql.DropRecoveryPhase), new("database_recovery_run_stats", SystemAdminSchemaSql.CreateRecoveryRunStats, SystemAdminSchemaSql.DropRecoveryRunStats), new("database_restore_point", SystemAdminSchemaSql.CreateRestorePoint, SystemAdminSchemaSql.DropRestorePoint), new("database_artifact_replica", SystemAdminSchemaSql.CreateArtifactReplica, SystemAdminSchemaSql.DropArtifactReplica), new("database_recovery_error", SystemAdminSchemaSql.CreateRecoveryError, SystemAdminSchemaSql.DropRecoveryError), new("database_backup_policy", SystemAdminSchemaSql.CreateBackupPolicy, SystemAdminSchemaSql.DropBackupPolicy), new("database_backup_service_health", SystemAdminSchemaSql.CreateServiceHealth, SystemAdminSchemaSql.DropServiceHealth), new("database_retention_state", SystemAdminSchemaSql.CreateRetentionState, SystemAdminSchemaSql.DropRetentionState), new("database_backup_projection_checkpoint", SystemAdminSchemaSql.CreateProjectionCheckpoint, SystemAdminSchemaSql.DropProjectionCheckpoint), new("database_backup_execution_outbox", SystemAdminSchemaSql.CreateExecutionOutbox, SystemAdminSchemaSql.DropExecutionOutbox), new("database_backup_projection_receipt", SystemAdminSchemaSql.CreateProjectionReceipt, SystemAdminSchemaSql.DropProjectionReceipt)];
    /// <inheritdoc/>
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}