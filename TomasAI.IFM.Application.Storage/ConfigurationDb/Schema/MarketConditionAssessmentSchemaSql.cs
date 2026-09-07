namespace TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;

public static class MarketConditionAssessmentSchemaSql
{
    public static readonly string Create = """
CREATE TABLE IF NOT EXISTS reference_configuration.market_condition_assessment_parameter_set (
    parameter_set_id uuid NOT NULL,
    version integer NOT NULL CHECK (version > 0),
    schema_version smallint NOT NULL CHECK (schema_version > 0),
    market_profile_id text NOT NULL,
    instrument_root text NOT NULL,
    target_horizon smallint NOT NULL,
    status smallint NOT NULL CHECK (status IN (0,1,2)),
    effective_from_utc timestamptz NULL,
    retired_at_utc timestamptz NULL,
    payload_json jsonb NOT NULL,
    payload_sha256 text NOT NULL CHECK (length(payload_sha256)=64),
    description text NOT NULL DEFAULT '',
    created_utc timestamptz NOT NULL,
    created_by text NOT NULL,
    PRIMARY KEY (parameter_set_id, version),
    CHECK ((status=0 AND effective_from_utc IS NULL AND retired_at_utc IS NULL) OR
           (status=1 AND effective_from_utc IS NOT NULL AND retired_at_utc IS NULL) OR
           (status=2 AND effective_from_utc IS NOT NULL AND retired_at_utc IS NOT NULL AND retired_at_utc >= effective_from_utc))
);
CREATE INDEX IF NOT EXISTS ix_market_condition_assessment_effective
ON reference_configuration.market_condition_assessment_parameter_set
(market_profile_id,instrument_root,target_horizon,status,effective_from_utc DESC);
""" + ConfigurationSchemaSql.CreateMarketConditionLifecycleGuard
        .Replace("market_condition_parameter_set", "market_condition_assessment_parameter_set", StringComparison.Ordinal)
        .Replace("IF OLD.parameter_set_id", "IF OLD.market_profile_id IS DISTINCT FROM NEW.market_profile_id OR\n       OLD.instrument_root IS DISTINCT FROM NEW.instrument_root OR\n       OLD.target_horizon IS DISTINCT FROM NEW.target_horizon OR\n       OLD.parameter_set_id", StringComparison.Ordinal);
}
