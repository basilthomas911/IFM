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

    /// <summary>Creates the composite effective-selection index required by Market Condition.</summary>
    public const string CreateMarketConditionEffectiveIndex = """
CREATE INDEX IF NOT EXISTS ix_market_condition_parameter_set_effective
ON reference_configuration.market_condition_parameter_set
((CAST(payload_json ->> 'FundId' AS integer)), (payload_json ->> 'InstrumentRoot'),
 (CAST(payload_json ->> 'TargetHorizon' AS smallint)), status, effective_from_utc DESC);
""";

    /// <summary>Adds lifecycle consistency constraints to new and previously provisioned Market Condition tables.</summary>
    public const string EnsureMarketConditionLifecycleConstraints = """
DO $migration$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_market_condition_parameter_set_status'
          AND conrelid = 'reference_configuration.market_condition_parameter_set'::regclass
    ) THEN
        ALTER TABLE reference_configuration.market_condition_parameter_set
        ADD CONSTRAINT ck_market_condition_parameter_set_status CHECK (status IN (0, 1, 2));
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_market_condition_parameter_set_lifecycle'
          AND conrelid = 'reference_configuration.market_condition_parameter_set'::regclass
    ) THEN
        ALTER TABLE reference_configuration.market_condition_parameter_set
        ADD CONSTRAINT ck_market_condition_parameter_set_lifecycle CHECK (
            (status = 0 AND effective_from_utc IS NULL AND retired_at_utc IS NULL) OR
            (status = 1 AND effective_from_utc IS NOT NULL AND retired_at_utc IS NULL) OR
            (status = 2 AND effective_from_utc IS NOT NULL AND retired_at_utc IS NOT NULL
                AND retired_at_utc >= effective_from_utc)
        );
    END IF;
END
$migration$;
""";

    /// <summary>Enforces the append-only Draft-to-Published-to-Retired lifecycle in PostgreSQL.</summary>
    public const string CreateMarketConditionLifecycleGuard = """
CREATE OR REPLACE FUNCTION reference_configuration.guard_market_condition_parameter_set()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'Market Condition parameter-set versions cannot be deleted.';
    END IF;

    IF OLD.parameter_set_id IS DISTINCT FROM NEW.parameter_set_id OR
       OLD.version IS DISTINCT FROM NEW.version OR
       OLD.schema_version IS DISTINCT FROM NEW.schema_version OR
       OLD.payload_json IS DISTINCT FROM NEW.payload_json OR
       OLD.payload_sha256 IS DISTINCT FROM NEW.payload_sha256 OR
       OLD.description IS DISTINCT FROM NEW.description OR
       OLD.created_utc IS DISTINCT FROM NEW.created_utc OR
       OLD.created_by IS DISTINCT FROM NEW.created_by THEN
        RAISE EXCEPTION 'Market Condition parameter-set version content is immutable.';
    END IF;

    IF OLD.status = 0 AND NEW.status = 1 AND
       OLD.effective_from_utc IS NULL AND NEW.effective_from_utc IS NOT NULL AND
       OLD.retired_at_utc IS NULL AND NEW.retired_at_utc IS NULL THEN
        RETURN NEW;
    END IF;

    IF OLD.status = 1 AND NEW.status = 2 AND
       NEW.effective_from_utc IS NOT DISTINCT FROM OLD.effective_from_utc AND
       OLD.retired_at_utc IS NULL AND NEW.retired_at_utc IS NOT NULL AND
       NEW.retired_at_utc >= NEW.effective_from_utc THEN
        RETURN NEW;
    END IF;

    RAISE EXCEPTION 'Invalid Market Condition parameter-set lifecycle transition.';
END
$function$;

DROP TRIGGER IF EXISTS trg_guard_market_condition_parameter_set
ON reference_configuration.market_condition_parameter_set;

CREATE TRIGGER trg_guard_market_condition_parameter_set
BEFORE UPDATE OR DELETE ON reference_configuration.market_condition_parameter_set
FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_market_condition_parameter_set();
""";
}
