namespace TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;

public static class VolatilitySeriesDefinitionSchemaSql
{
    public const string Create = """
CREATE TABLE IF NOT EXISTS reference_configuration.volatility_series_definition(
    series_id text NOT NULL,
    methodology_version text NOT NULL,
    environment text NOT NULL,
    effective_from_utc timestamptz NOT NULL,
    effective_until_utc timestamptz NULL,
    payload_json jsonb NOT NULL,
    payload_sha256 char(64) NOT NULL,
    approved_configuration_version text NOT NULL,
    owner text NOT NULL,
    approval_evidence_id text NOT NULL,
    created_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY(series_id,methodology_version),
    CHECK(effective_until_utc IS NULL OR effective_until_utc > effective_from_utc)
);
CREATE INDEX IF NOT EXISTS ix_volatility_series_definition_effective
ON reference_configuration.volatility_series_definition(environment,series_id,effective_from_utc DESC);
CREATE OR REPLACE FUNCTION reference_configuration.guard_volatility_series_definition()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'Volatility series definitions are immutable and append-only.';
END $$;
DROP TRIGGER IF EXISTS trg_guard_volatility_series_definition
ON reference_configuration.volatility_series_definition;
CREATE TRIGGER trg_guard_volatility_series_definition
BEFORE UPDATE OR DELETE ON reference_configuration.volatility_series_definition
FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_volatility_series_definition();
""";

    public const string Drop = """
DROP TRIGGER IF EXISTS trg_guard_volatility_series_definition
ON reference_configuration.volatility_series_definition;
DROP FUNCTION IF EXISTS reference_configuration.guard_volatility_series_definition();
DROP TABLE IF EXISTS reference_configuration.volatility_series_definition;
""";
}
