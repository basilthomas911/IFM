namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

internal static class ConfigurationDbSql
{
    public const string InsertDraft = """
INSERT INTO reference_configuration.regime_discovery_parameter_set
    (parameter_set_id, version, schema_version, status, effective_from_utc, retired_at_utc,
     payload_json, payload_sha256, description, created_utc, created_by)
VALUES ($1, $2, $3, $4, NULL, NULL,
        CAST($5 AS jsonb), $6, $7, $8, $9);
""";

    public const string Publish = """
UPDATE reference_configuration.regime_discovery_parameter_set
SET status = $1, effective_from_utc = $2
WHERE parameter_set_id = $3 AND version = $4 AND status = $5;
""";

    public const string Retire = """
UPDATE reference_configuration.regime_discovery_parameter_set
SET status = $1, retired_at_utc = $2
WHERE parameter_set_id = $3 AND version = $4 AND status = $5;
""";

    public const string GetExact = """
SELECT parameter_set_id, version, schema_version, status, effective_from_utc, retired_at_utc,
       payload_json::text, payload_sha256, description, created_utc, created_by
FROM reference_configuration.regime_discovery_parameter_set
WHERE parameter_set_id = $1 AND version = $2;
""";

    public const string ResolveEffective = """
SELECT parameter_set_id, version, schema_version, status, effective_from_utc, retired_at_utc,
       payload_json::text, payload_sha256, description, created_utc, created_by
FROM reference_configuration.regime_discovery_parameter_set
WHERE status = $1
  AND effective_from_utc <= $2
  AND CAST(payload_json ->> 'TargetHorizon' AS smallint) = $3
  AND (retired_at_utc IS NULL OR retired_at_utc > $4)
ORDER BY effective_from_utc DESC, parameter_set_id, version DESC
LIMIT 2;
""";
}
