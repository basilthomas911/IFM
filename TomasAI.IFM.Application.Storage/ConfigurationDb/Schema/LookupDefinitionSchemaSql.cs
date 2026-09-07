namespace TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;

public static class LookupDefinitionSchemaSql
{
    public const string Create = """
CREATE TABLE IF NOT EXISTS reference_configuration.lookup_definition (
    id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    group_name varchar(64) NOT NULL CHECK(group_name ~ '^[A-Za-z][A-Za-z0-9]*$'),
    internal_value varchar(64) NOT NULL CHECK(internal_value ~ '^[A-Za-z][A-Za-z0-9]*$'),
    display_name varchar(100) NOT NULL CHECK(length(trim(display_name))>0),
    description text NOT NULL DEFAULT '',
    display_order integer NOT NULL DEFAULT 0 CHECK(display_order>=0),
    is_enabled boolean NOT NULL DEFAULT true,
    created_utc timestamptz NOT NULL DEFAULT now(),
    updated_utc timestamptz NOT NULL DEFAULT now(),
    UNIQUE(group_name,internal_value)
);
CREATE INDEX IF NOT EXISTS ix_lookup_definition_group_order
    ON reference_configuration.lookup_definition(group_name,display_order,id);
CREATE OR REPLACE FUNCTION reference_configuration.guard_lookup_definition() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF (NEW.id,NEW.group_name,NEW.internal_value,NEW.created_utc) IS DISTINCT FROM (OLD.id,OLD.group_name,OLD.internal_value,OLD.created_utc)
        THEN RAISE EXCEPTION 'Lookup identity and internal value are immutable'; END IF;
    NEW.updated_utc = clock_timestamp();
    RETURN NEW;
END $$;
CREATE OR REPLACE TRIGGER guard_lookup_definition BEFORE UPDATE ON reference_configuration.lookup_definition
FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_lookup_definition();
INSERT INTO reference_configuration.lookup_definition(group_name,internal_value,display_name,description,display_order) VALUES
('AssetTypes','Futures','Futures','Futures contracts.',10),
('AssetTypes','FuturesOption','Futures Options','Options on futures contracts.',20),
('Directions','Bullish','Bullish','Upward market direction.',10),
('Directions','Bearish','Bearish','Downward market direction.',20),
('Directions','Neutral','Neutral','Neutral market direction.',30),
('MarketConditions','Directional','Directional','Directional market condition.',10),
('MarketConditions','RangeBound','Range Bound','Market trading within a range.',20),
('MarketConditions','Transition','Transition','Market transitioning between conditions.',30),
('MarketConditions','VolatilityExpansion','Volatility Expansion','Expanding market volatility.',40),
('MarketConditions','VolatilityContraction','Volatility Contraction','Contracting market volatility.',50),
('MarketConditions','Dislocated','Dislocated','Dislocated market; tradeability rules still apply.',60),
('MarketConditions','NoOpportunity','No Opportunity','No market opportunity; this permission does not override tradeability.',70)
ON CONFLICT(group_name,internal_value) DO NOTHING;
""";
    public const string Drop = """
DROP TABLE IF EXISTS reference_configuration.lookup_definition;
DROP FUNCTION IF EXISTS reference_configuration.guard_lookup_definition();
""";
}
