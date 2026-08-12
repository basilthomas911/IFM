namespace TomasAI.IFM.Application.Storage;

public static class SystemAdminDbSql
{
    public const string GetProjectionReceiptForUpdate = """
SELECT event_hash
FROM system_admin.database_backup_projection_receipt_v1
WHERE projector_name = $1 AND event_id = $2
FOR UPDATE;
""";

    public const string InsertProjectionReceipt = """
INSERT INTO system_admin.database_backup_projection_receipt_v1
    (projector_name, event_id, event_hash, source_event_id, applied_utc)
VALUES ($1, $2, $3, $4, $5);
""";

    public const string UpsertProjectionCheckpoint = """
INSERT INTO system_admin.database_backup_projection_checkpoint_v1
    (projector_name, last_event_id, applied_count, updated_utc)
VALUES ($1, $2, 1, $3)
ON CONFLICT (projector_name) DO UPDATE SET
    last_event_id = GREATEST(system_admin.database_backup_projection_checkpoint_v1.last_event_id, EXCLUDED.last_event_id),
    applied_count = system_admin.database_backup_projection_checkpoint_v1.applied_count + 1,
    updated_utc = EXCLUDED.updated_utc;
""";

    public const string GetProjectionCheckpoint = """
SELECT projector_name, last_event_id, applied_count, updated_utc
FROM system_admin.database_backup_projection_checkpoint_v1
WHERE projector_name = $1;
""";

    public const string UpsertOperation = """
INSERT INTO system_admin.database_recovery_operation_v1
    (operation_id, backup_set_id, protection_set_id, source, operation_kind, phase, outcome,
     progress_percent, state_revision, created_utc, completed_utc, safe_diagnostic_reference,
     restore_point_id, restore_class, fresh_target_profile, validation_revision, cutover_state,
     policy_revision, last_event_id, last_source_event_id)
VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20)
ON CONFLICT (operation_id) DO UPDATE SET
    backup_set_id = COALESCE(EXCLUDED.backup_set_id, system_admin.database_recovery_operation_v1.backup_set_id),
    protection_set_id = EXCLUDED.protection_set_id,
    source = EXCLUDED.source,
    operation_kind = EXCLUDED.operation_kind,
    phase = EXCLUDED.phase,
    outcome = EXCLUDED.outcome,
    progress_percent = EXCLUDED.progress_percent,
    state_revision = EXCLUDED.state_revision,
    completed_utc = COALESCE(EXCLUDED.completed_utc, system_admin.database_recovery_operation_v1.completed_utc),
    safe_diagnostic_reference = CASE WHEN EXCLUDED.safe_diagnostic_reference = '' THEN system_admin.database_recovery_operation_v1.safe_diagnostic_reference ELSE EXCLUDED.safe_diagnostic_reference END,
    restore_point_id = COALESCE(EXCLUDED.restore_point_id, system_admin.database_recovery_operation_v1.restore_point_id),
    restore_class = CASE WHEN EXCLUDED.restore_class = 0 THEN system_admin.database_recovery_operation_v1.restore_class ELSE EXCLUDED.restore_class END,
    fresh_target_profile = CASE WHEN EXCLUDED.fresh_target_profile = '' THEN system_admin.database_recovery_operation_v1.fresh_target_profile ELSE EXCLUDED.fresh_target_profile END,
    validation_revision = GREATEST(system_admin.database_recovery_operation_v1.validation_revision, EXCLUDED.validation_revision),
    cutover_state = CASE WHEN EXCLUDED.cutover_state = 0 THEN system_admin.database_recovery_operation_v1.cutover_state ELSE EXCLUDED.cutover_state END,
    policy_revision = GREATEST(system_admin.database_recovery_operation_v1.policy_revision, EXCLUDED.policy_revision),
    last_event_id = EXCLUDED.last_event_id,
    last_source_event_id = EXCLUDED.last_source_event_id
WHERE EXCLUDED.state_revision > system_admin.database_recovery_operation_v1.state_revision;
""";

    public const string InsertPhase = """
INSERT INTO system_admin.database_recovery_phase_v1
    (operation_id, phase, event_revision, outcome, progress_percent, observed_utc, host_id, last_event_id, last_source_event_id)
VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9)
ON CONFLICT (operation_id, phase, event_revision) DO NOTHING;
""";

    public const string UpsertRestorePoint = """
INSERT INTO system_admin.database_restore_point_v1
    (restore_point_id, source, backup_set_id, protection_set_id, recovery_point_utc,
     verification_level, verified_utc, restore_tested_utc, eligible, legal_hold,
     manifest_revision, source_revision, last_event_id, last_source_event_id)
VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14)
ON CONFLICT (restore_point_id, source) DO UPDATE SET
    backup_set_id = COALESCE(EXCLUDED.backup_set_id, system_admin.database_restore_point_v1.backup_set_id),
    verification_level = GREATEST(system_admin.database_restore_point_v1.verification_level, EXCLUDED.verification_level),
    verified_utc = COALESCE(EXCLUDED.verified_utc, system_admin.database_restore_point_v1.verified_utc),
    restore_tested_utc = COALESCE(EXCLUDED.restore_tested_utc, system_admin.database_restore_point_v1.restore_tested_utc),
    eligible = EXCLUDED.eligible OR system_admin.database_restore_point_v1.eligible,
    legal_hold = EXCLUDED.legal_hold,
    manifest_revision = GREATEST(system_admin.database_restore_point_v1.manifest_revision, EXCLUDED.manifest_revision),
    source_revision = EXCLUDED.source_revision,
    last_event_id = EXCLUDED.last_event_id,
    last_source_event_id = EXCLUDED.last_source_event_id
WHERE EXCLUDED.source_revision > system_admin.database_restore_point_v1.source_revision;
""";

    public const string UpsertArtifactReplica = """
INSERT INTO system_admin.database_artifact_replica_v1
    (artifact_replica_id, source, operation_id, artifact_id, engine, replica_state,
     safe_destination_reference, bytes, source_revision, last_event_id, last_source_event_id)
VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11)
ON CONFLICT (artifact_replica_id, source) DO UPDATE SET
    replica_state = EXCLUDED.replica_state,
    safe_destination_reference = EXCLUDED.safe_destination_reference,
    bytes = COALESCE(EXCLUDED.bytes, system_admin.database_artifact_replica_v1.bytes),
    source_revision = EXCLUDED.source_revision,
    last_event_id = EXCLUDED.last_event_id,
    last_source_event_id = EXCLUDED.last_source_event_id
WHERE EXCLUDED.source_revision > system_admin.database_artifact_replica_v1.source_revision;
""";

    public const string UpsertRecoveryError = """
INSERT INTO system_admin.database_recovery_error_v1
    (operation_id, error_identity, classification, safe_diagnostic_reference, observed_utc,
     occurrence_count, source_revision, last_event_id, last_source_event_id)
VALUES ($1,$2,$3,$4,$5,1,$6,$7,$8)
ON CONFLICT (operation_id, error_identity) DO UPDATE SET
    occurrence_count = system_admin.database_recovery_error_v1.occurrence_count + 1,
    classification = EXCLUDED.classification,
    safe_diagnostic_reference = EXCLUDED.safe_diagnostic_reference,
    source_revision = EXCLUDED.source_revision,
    last_event_id = EXCLUDED.last_event_id,
    last_source_event_id = EXCLUDED.last_source_event_id
WHERE EXCLUDED.source_revision > system_admin.database_recovery_error_v1.source_revision;
""";

    public const string UpsertPolicy = """
INSERT INTO system_admin.database_backup_policy_v1
    (environment_identity, policy_id, policy_revision, definition_json, enforced,
     source_revision, last_event_id, last_source_event_id)
VALUES ($1,$2,$3,$4::jsonb,$5,$6,$7,$8)
ON CONFLICT (environment_identity, policy_id) DO UPDATE SET
    policy_revision = EXCLUDED.policy_revision,
    definition_json = EXCLUDED.definition_json,
    enforced = EXCLUDED.enforced,
    source_revision = EXCLUDED.source_revision,
    last_event_id = EXCLUDED.last_event_id,
    last_source_event_id = EXCLUDED.last_source_event_id
WHERE EXCLUDED.source_revision > system_admin.database_backup_policy_v1.source_revision;
""";

    public const string UpsertServiceHealth = """
INSERT INTO system_admin.database_backup_service_health_v1
    (environment_identity, source, host_id, capability_state, ready, last_service_sequence,
     observed_utc, safe_diagnostic_reference, reconciled, source_revision, last_event_id, last_source_event_id)
VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)
ON CONFLICT (environment_identity, source, host_id) DO UPDATE SET
    capability_state = EXCLUDED.capability_state,
    ready = EXCLUDED.ready,
    last_service_sequence = EXCLUDED.last_service_sequence,
    observed_utc = EXCLUDED.observed_utc,
    safe_diagnostic_reference = EXCLUDED.safe_diagnostic_reference,
    reconciled = EXCLUDED.reconciled OR system_admin.database_backup_service_health_v1.reconciled,
    source_revision = EXCLUDED.source_revision,
    last_event_id = EXCLUDED.last_event_id,
    last_source_event_id = EXCLUDED.last_source_event_id
WHERE EXCLUDED.source_revision > system_admin.database_backup_service_health_v1.source_revision;
""";

    public const string UpsertRetention = """
INSERT INTO system_admin.database_retention_state_v1
    (plan_id, source, plan_revision, evaluation_boundary_utc, retain_json, delete_json,
     approved, outcome, source_revision, last_event_id, last_source_event_id)
VALUES ($1,$2,$3,$4,$5::jsonb,$6::jsonb,$7,$8,$9,$10,$11)
ON CONFLICT (plan_id, source) DO UPDATE SET
    plan_revision = EXCLUDED.plan_revision,
    evaluation_boundary_utc = EXCLUDED.evaluation_boundary_utc,
    retain_json = EXCLUDED.retain_json,
    delete_json = EXCLUDED.delete_json,
    approved = EXCLUDED.approved,
    outcome = EXCLUDED.outcome,
    source_revision = EXCLUDED.source_revision,
    last_event_id = EXCLUDED.last_event_id,
    last_source_event_id = EXCLUDED.last_source_event_id
WHERE EXCLUDED.source_revision > system_admin.database_retention_state_v1.source_revision;
""";

    public const string InsertRunStatistics = """
INSERT INTO system_admin.database_recovery_run_stats_v1
    (operation_id, source, phase, engine, statistics_revision, started_utc, completed_utc,
     elapsed_ticks, source_bytes, stored_bytes, transferred_bytes, restored_bytes,
     artifact_count, average_throughput, peak_throughput, retry_count, warning_count,
     achieved_rpo_ticks, achieved_rto_ticks, host_id, policy_revision, last_event_id, last_source_event_id)
VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22,$23)
ON CONFLICT (operation_id, phase, engine, statistics_revision) DO NOTHING;
""";

    public const string GetProtectionSets = """
SELECT protection_set_id, source, MAX(policy_revision)
FROM system_admin.database_recovery_operation_v1
WHERE ($1 = 0 OR source = $1)
GROUP BY protection_set_id, source
ORDER BY protection_set_id, source;
""";

    public const string GetPolicy = """
SELECT policy_id, environment_identity, policy_revision, definition_json::text, enforced
FROM system_admin.database_backup_policy_v1
WHERE environment_identity = $1 AND policy_id = $2;
""";

    public const string OperationColumns = """
operation_id, backup_set_id, protection_set_id, source, operation_kind, phase, outcome,
progress_percent, state_revision, created_utc, completed_utc, safe_diagnostic_reference,
restore_point_id, restore_class, fresh_target_profile, validation_revision, cutover_state
""";

    public static readonly string GetOperation = $"SELECT {OperationColumns} FROM system_admin.database_recovery_operation_v1 WHERE operation_id = $1;";
    public static readonly string ListOperations = $"""
SELECT {OperationColumns}
FROM system_admin.database_recovery_operation_v1
WHERE ($1 = 0 OR source = $1)
  AND ($2 IS NULL OR protection_set_id = $2)
  AND ($3 IS NULL OR created_utc >= $3)
  AND ($4 IS NULL OR created_utc <= $4)
  AND ($5 IS NULL OR operation_id > $5)
ORDER BY operation_id
LIMIT $6;
""";
    public static readonly string GetBackupSetOperations = $"SELECT {OperationColumns} FROM system_admin.database_recovery_operation_v1 WHERE backup_set_id = $1 ORDER BY operation_id;";
    public static readonly string GetRestoreOperation = $"SELECT {OperationColumns} FROM system_admin.database_recovery_operation_v1 WHERE operation_id = $1 AND operation_kind IN (3,4,5);";
    public static readonly string ListRestoreDrills = $"SELECT {OperationColumns} FROM system_admin.database_recovery_operation_v1 WHERE operation_kind = 4 AND ($1 = 0 OR source = $1) ORDER BY created_utc DESC LIMIT $2;";

    public const string RestorePointColumns = """
restore_point_id, backup_set_id, protection_set_id, source, recovery_point_utc,
verification_level, verified_utc, restore_tested_utc, eligible, legal_hold, manifest_revision
""";
    public static readonly string ListRestorePoints = $"""
SELECT {RestorePointColumns}
FROM system_admin.database_restore_point_v1
WHERE ($1 = 0 OR source = $1)
  AND ($2 IS NULL OR protection_set_id = $2)
  AND ($3 IS NULL OR recovery_point_utc >= $3)
  AND ($4 IS NULL OR recovery_point_utc <= $4)
  AND ($5 IS NULL OR restore_point_id > $5)
ORDER BY restore_point_id
LIMIT $6;
""";
    public static readonly string GetRestorePoint = $"SELECT {RestorePointColumns} FROM system_admin.database_restore_point_v1 WHERE restore_point_id = $1 AND source = $2;";
    public static readonly string GetLatestVerified = $"SELECT {RestorePointColumns} FROM system_admin.database_restore_point_v1 WHERE source = $1 AND protection_set_id = $2 AND eligible AND verified_utc IS NOT NULL ORDER BY recovery_point_utc DESC LIMIT 1;";
    public static readonly string GetLatestRestoreTested = $"SELECT {RestorePointColumns} FROM system_admin.database_restore_point_v1 WHERE source = $1 AND protection_set_id = $2 AND eligible AND restore_tested_utc IS NOT NULL ORDER BY recovery_point_utc DESC LIMIT 1;";

    public const string GetRetention = """
SELECT plan_id, source, plan_revision, evaluation_boundary_utc, retain_json::text,
       delete_json::text, approved, outcome
FROM system_admin.database_retention_state_v1
WHERE ($1 = 0 OR source = $1) AND ($2 IS NULL OR plan_id = $2)
ORDER BY evaluation_boundary_utc DESC
LIMIT 1;
""";

    public const string GetServiceHealth = """
SELECT source, host_id, capability_state, ready, last_service_sequence, observed_utc,
       safe_diagnostic_reference
FROM system_admin.database_backup_service_health_v1
WHERE environment_identity = $1 AND ($2 = 0 OR source = $2)
ORDER BY source, host_id;
""";

    public const string GetRunStatistics = """
SELECT source, statistics_revision, engine, phase, started_utc, completed_utc, elapsed_ticks,
       source_bytes, stored_bytes, transferred_bytes, restored_bytes, artifact_count,
       average_throughput, peak_throughput, retry_count, warning_count,
       achieved_rpo_ticks, achieved_rto_ticks
FROM system_admin.database_recovery_run_stats_v1
WHERE operation_id = $1
ORDER BY statistics_revision, phase, engine;
""";

    public const string ClearProjections = """
TRUNCATE TABLE
    system_admin.database_recovery_phase_v1,
    system_admin.database_recovery_run_stats_v1,
    system_admin.database_restore_point_v1,
    system_admin.database_artifact_replica_v1,
    system_admin.database_recovery_error_v1,
    system_admin.database_backup_policy_v1,
    system_admin.database_backup_service_health_v1,
    system_admin.database_retention_state_v1,
    system_admin.database_recovery_operation_v1;
""";

    public const string ClearProjectionReceipts = "DELETE FROM system_admin.database_backup_projection_receipt_v1 WHERE projector_name = $1;";
    public const string ClearProjectionCheckpoint = "DELETE FROM system_admin.database_backup_projection_checkpoint_v1 WHERE projector_name = $1;";
}
