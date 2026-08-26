namespace TomasAI.IFM.Application.Storage.HistoricalBootstrap;

/// <summary>Contains PostgreSQL statements for resumable market-data history bootstraps.</summary>
public static class HistoricalBootstrapSql
{
    /// <summary>Creates or advances a checkpoint and stores its immutable terminal manifest.</summary>
    public const string Save = """
    INSERT INTO historical_bootstrap_checkpoint (
        bootstrap_attempt_id, request_sha256, status, stage, provider_job_id,
        provider_file_id, batch_ordinal, source_position, error_message, updated_at_utc)
    VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
    ON CONFLICT (bootstrap_attempt_id) DO UPDATE SET
        request_sha256 = EXCLUDED.request_sha256,
        status = EXCLUDED.status,
        stage = EXCLUDED.stage,
        provider_job_id = EXCLUDED.provider_job_id,
        provider_file_id = EXCLUDED.provider_file_id,
        batch_ordinal = EXCLUDED.batch_ordinal,
        source_position = EXCLUDED.source_position,
        error_message = EXCLUDED.error_message,
        updated_at_utc = EXCLUDED.updated_at_utc;

    INSERT INTO historical_bootstrap_manifest (
        bootstrap_attempt_id, manifest_json, audit_json, created_at_utc)
    SELECT $1, $11, $12, $10
    WHERE $11 <> ''
    ON CONFLICT (bootstrap_attempt_id) DO NOTHING;
    """;

    /// <summary>Reads one exact attempt.</summary>
    public const string GetByAttempt = """
    SELECT c.bootstrap_attempt_id, c.request_sha256, c.status, c.stage,
           c.provider_job_id, c.provider_file_id, c.batch_ordinal,
           c.source_position, c.error_message, c.updated_at_utc,
           m.manifest_json, m.audit_json
    FROM historical_bootstrap_checkpoint c
    LEFT JOIN historical_bootstrap_manifest m
      ON m.bootstrap_attempt_id = c.bootstrap_attempt_id
    WHERE c.bootstrap_attempt_id = $1;
    """;

    /// <summary>Reads the immutable completed owner of one request hash.</summary>
    public const string GetCompletedByRequestHash = """
    SELECT c.bootstrap_attempt_id, c.request_sha256, c.status, c.stage,
           c.provider_job_id, c.provider_file_id, c.batch_ordinal,
           c.source_position, c.error_message, c.updated_at_utc,
           m.manifest_json, m.audit_json
    FROM historical_bootstrap_checkpoint c
    INNER JOIN historical_bootstrap_manifest m
      ON m.bootstrap_attempt_id = c.bootstrap_attempt_id
    WHERE c.request_sha256 = $1 AND c.status = 2
    LIMIT 1;
    """;
}
