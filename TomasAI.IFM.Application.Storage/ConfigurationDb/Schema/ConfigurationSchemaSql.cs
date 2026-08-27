namespace TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;

/// <summary>Defines the PostgreSQL strategy-configuration schema.</summary>
public static class ConfigurationSchemaSql
{
    /// <summary>Creates the owning PostgreSQL schema.</summary>
    public const string CreateSchema = "CREATE SCHEMA IF NOT EXISTS reference_configuration;";

    /// <summary>Creates an immutable parameter-set table with guarded lifecycle metadata.</summary>
    public static string CreateTable(string tableName) => $"""
CREATE TABLE IF NOT EXISTS reference_configuration.{tableName} (
    parameter_set_id uuid NOT NULL,
    version integer NOT NULL,
    schema_version smallint NOT NULL,
    status smallint NOT NULL,
    effective_from_utc timestamptz NULL,
    retired_at_utc timestamptz NULL,
    payload_json jsonb NOT NULL,
    payload_sha256 text NOT NULL,
    description text NOT NULL DEFAULT '',
    created_utc timestamptz NOT NULL,
    created_by text NOT NULL,
    CONSTRAINT pk_{tableName} PRIMARY KEY (parameter_set_id, version),
    CONSTRAINT ck_{tableName}_version CHECK (version > 0),
    CONSTRAINT ck_{tableName}_schema_version CHECK (schema_version > 0),
    CONSTRAINT ck_{tableName}_hash CHECK (length(payload_sha256) = 64)
);
CREATE INDEX IF NOT EXISTS ix_{tableName}_effective
ON reference_configuration.{tableName} (status, effective_from_utc DESC);
CREATE INDEX IF NOT EXISTS ix_{tableName}_target_horizon_effective
ON reference_configuration.{tableName}
((CAST(payload_json ->> 'TargetHorizon' AS smallint)), status, effective_from_utc DESC);
""";
}
