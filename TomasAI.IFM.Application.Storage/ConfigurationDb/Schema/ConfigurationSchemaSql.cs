using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
/// <summary>Defines the PostgreSQL strategy-configuration schema.</summary>
public static class ConfigurationSchemaSql
{
    /// <summary>Typed scalar and ordered child tables for the closed parameter contract.</summary>
    internal static class TypedParameters
    {
        internal const string CreateHeader = """
            CREATE TABLE IF NOT EXISTS reference_configuration.parameter_value_version (
                set_id uuid NOT NULL, version integer NOT NULL CHECK(version>0),
                component_code text NOT NULL, schema_version integer NOT NULL CHECK(schema_version>0),
                content_sha256 text NOT NULL CHECK(length(content_sha256)=64), codec text NOT NULL,
                PRIMARY KEY(set_id,version));
            """;
        internal static string Create(ParameterValueMapping.Table table) => $"""
            CREATE TABLE IF NOT EXISTS reference_configuration.{table.Name} (
                owner_set_id uuid NOT NULL, owner_version integer NOT NULL,
                row_path text NOT NULL, parent_path text NULL, ordinal integer NOT NULL CHECK(ordinal>=0),
                {string.Join(",",table.Columns.Select(x=>$"{x.Name} {ParameterValueMapping.SqlType(x.Type)}"))},
                PRIMARY KEY(owner_set_id,owner_version,row_path),
                FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
                {(table.Parent is null ? "CHECK(row_path='$' AND parent_path IS NULL AND ordinal=0)" : $"FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.{table.Parent.Name}(owner_set_id,owner_version,row_path), UNIQUE(owner_set_id,owner_version,parent_path,ordinal)")});
            """;
        internal static IEnumerable<SchemaObjectDefinition> Definitions =>
            new[] { new SchemaObjectDefinition("parameter_value_version",CreateHeader,"DROP TABLE IF EXISTS reference_configuration.parameter_value_version;") }
                .Concat(ParameterValueMapping.Tables.Select(table=>new SchemaObjectDefinition(table.Name,Create(table),$"DROP TABLE IF EXISTS reference_configuration.{table.Name};")));
    }


    internal static class TypedCatalog
    {
        internal const string Header = """
            CREATE TABLE IF NOT EXISTS reference_configuration.parameter_projection_record(
                kind text NOT NULL,owner_id uuid NOT NULL,owner_version bigint NOT NULL,content_sha256 text NOT NULL,
                PRIMARY KEY(kind,owner_id,owner_version),CHECK(length(content_sha256)=64));
            CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_operation(
                operation_id uuid PRIMARY KEY,entity_id uuid NOT NULL,revision bigint NOT NULL,request_sha256 text NOT NULL,
                kind text NOT NULL,version integer NOT NULL,committed_at_ticks bigint NOT NULL,fact_sha256 text NOT NULL,UNIQUE(kind,entity_id,revision));
            CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_migration(
                migration_id smallint PRIMARY KEY,source_cut bigint NOT NULL);
            CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_startup_state(
                run_id uuid PRIMARY KEY,revision bigint NOT NULL,active boolean NOT NULL);
            """;
        internal static string Create(ParameterValueMapping.Table table)
        {
            var root=table;while(root.Parent is not null)root=root.Parent;
            return $"""
                CREATE TABLE IF NOT EXISTS reference_configuration.{table.Name}(
                    owner_set_id uuid NOT NULL,owner_version bigint NOT NULL,row_path text NOT NULL,parent_path text NULL,
                    ordinal integer NOT NULL CHECK(ordinal>=0),owner_kind text NOT NULL DEFAULT '{root.Name}' CHECK(owner_kind='{root.Name}'),
                    {string.Join(",",table.Columns.Select(x=>$"{x.Name} {ParameterValueMapping.SqlType(x.Type)}"))},
                    PRIMARY KEY(owner_set_id,owner_version,row_path),
                    FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
                    {(table.Parent is null ? "CHECK(row_path='$' AND parent_path IS NULL AND ordinal=0)" : $"FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.{table.Parent.Name}(owner_set_id,owner_version,row_path),UNIQUE(owner_set_id,owner_version,parent_path,ordinal)")});
                """;
        }
        internal static IEnumerable<SchemaObjectDefinition> Definitions=>
            new[]{new SchemaObjectDefinition("parameter_projection_record",Header,"DROP TABLE IF EXISTS reference_configuration.parameter_catalog_startup_state; DROP TABLE IF EXISTS reference_configuration.parameter_catalog_operation; DROP TABLE IF EXISTS reference_configuration.parameter_projection_record;")}
                .Concat(ParameterProjectionMapping.Tables.Select(table=>new SchemaObjectDefinition(table.Name,Create(table),$"DROP TABLE IF EXISTS reference_configuration.{table.Name};")));
    }
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
    /// <summary>Defines PostgreSQL lookups schema objects.</summary>
    public static class Lookups
    {
        public const string Create = CreateStructure + Seed;
        /// <summary>Creates lookup storage and guards without writing default rows during reconstruction.</summary>
        public const string CreateStructure = """
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
""";
        /// <summary>Seeds default lookup rows only during ordinary initialization.</summary>
        public const string Seed = """
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

    /// <summary>Defines PostgreSQL market condition assessment schema objects.</summary>
    public static class MarketConditionAssessment
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
""" + ConfigurationSchemaSql.CreateMarketConditionLifecycleGuard.Replace("market_condition_parameter_set", "market_condition_assessment_parameter_set", StringComparison.Ordinal).Replace("IF OLD.parameter_set_id", "IF OLD.market_profile_id IS DISTINCT FROM NEW.market_profile_id OR\n       OLD.instrument_root IS DISTINCT FROM NEW.instrument_root OR\n       OLD.target_horizon IS DISTINCT FROM NEW.target_horizon OR\n       OLD.parameter_set_id", StringComparison.Ordinal);
    }

    /// <summary>Defines PostgreSQL strategy catalog schema objects.</summary>
    internal static class StrategyCatalog
    {
        internal const long WriterLock = 497003170627;
        internal const string Prefix = "reference_configuration.";
        internal static readonly CatalogChildTable[] Children = [Relation("strategy_family_membership", "Families", 2, 1), Relation("strategy_structure_assignment", "Structures", 2, 3), Relation("strategy_deployment_structure_variant", "Variants", 7, 4), new("strategy_capability_requirement", "Capabilities", "owner_kind <> 1", "role text NOT NULL CHECK(role IN ('evaluator','builder','validator','risk','data')), code text NOT NULL, capability_version integer NOT NULL CHECK(capability_version>0)", "role,code,capability_version", "role,code,capability_version", "j->>'Role',j->>'Code',(j->>'Version')::integer", "jsonb_build_object('Role',role,'Code',code,'Version',capability_version)"), new("structure_expiry_group", "ExpiryGroups", "owner_kind=3", "group_key text NOT NULL, after_group text NULL", "group_key", "group_key,after_group", "j->>'Key',j->>'AfterGroup'", "jsonb_build_object('Key',group_key,'AfterGroup',after_group)", ", FOREIGN KEY(owner_kind,owner_id,owner_version,after_group) REFERENCES reference_configuration.structure_expiry_group(owner_kind,owner_id,owner_version,group_key) DEFERRABLE INITIALLY DEFERRED"), new("structure_leg_definition", "Legs", "owner_kind=3", "leg_key text NOT NULL, instrument_class text NOT NULL CHECK(instrument_class IN ('Futures','FuturesOption')), side text NOT NULL CHECK(side IN ('Buy','Sell')), option_right text NOT NULL, ratio numeric NOT NULL CHECK(ratio>0 AND ratio<=1000000), expiry_group text NOT NULL", "leg_key", "leg_key,instrument_class,side,option_right,ratio,expiry_group", "j->>'Key',j->>'InstrumentClass',j->>'Side',j->>'OptionRight',(j->>'Ratio')::numeric,j->>'ExpiryGroup'", "jsonb_build_object('Key',leg_key,'InstrumentClass',instrument_class,'Side',side,'OptionRight',option_right,'Ratio',ratio,'ExpiryGroup',expiry_group)", ", CHECK((instrument_class='Futures' AND option_right='None') OR (instrument_class='FuturesOption' AND option_right IN ('Call','Put'))), FOREIGN KEY(owner_kind,owner_id,owner_version,expiry_group) REFERENCES reference_configuration.structure_expiry_group(owner_kind,owner_id,owner_version,group_key)"), new("structure_variant_leg_rule", "VariantLegs", "owner_kind=4", "leg_key text NOT NULL, side text NOT NULL CHECK(side IN ('Buy','Sell')), ratio numeric NOT NULL CHECK(ratio>0 AND ratio<=1000000), structure_kind smallint NOT NULL CHECK(structure_kind=3), structure_id uuid NOT NULL, structure_version integer NOT NULL", "leg_key", "leg_key,side,ratio,structure_kind,structure_id,structure_version", "j->>'LegKey',j->>'Side',(j->>'Ratio')::numeric,3,(d->'Parent'->>'Id')::uuid,(d->'Parent'->>'Version')::integer", "jsonb_build_object('LegKey',leg_key,'Side',side,'Ratio',ratio)", ", FOREIGN KEY(structure_kind,structure_id,structure_version,leg_key) REFERENCES reference_configuration.structure_leg_definition(owner_kind,owner_id,owner_version,leg_key), FOREIGN KEY(owner_kind,owner_id,owner_version,structure_kind,structure_id,structure_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version,parent_kind,parent_id,parent_version)"), new("strategy_deployment_product", "Products", "owner_kind=7", "product_id integer NOT NULL CHECK(product_id>0), symbol text NOT NULL, exchange text NOT NULL, currency text NOT NULL", "product_id", "product_id,symbol,exchange,currency", "(j->>'ProductId')::integer,j->>'Symbol',j->>'Exchange',j->>'Currency'", "jsonb_build_object('ProductId',product_id,'Symbol',symbol,'Exchange',exchange,'Currency',currency)"), new("strategy_deployment_parameter_binding", "PipelineParameters", "owner_kind=7", "role text NOT NULL, parameter_kind smallint NOT NULL CHECK(parameter_kind BETWEEN 1 AND 7), parameter_id uuid NOT NULL, parameter_version integer NOT NULL CHECK(parameter_version>0), payload_sha256 text NOT NULL CHECK(payload_sha256 ~ '^[0-9a-f]{64}$')", "role", "role,parameter_kind,parameter_id,parameter_version,payload_sha256", "j->>'Role',(j->>'Kind')::smallint,(j->>'Id')::uuid,(j->>'Version')::integer,j->>'Hash'", "jsonb_build_object('Role',role,'Kind',parameter_kind,'Id',parameter_id,'Version',parameter_version,'Hash',payload_sha256)"), new("strategy_deployment_catalog_parameter_binding", "Parameters", "owner_kind=7", "role text NOT NULL, parameter_kind smallint NOT NULL CHECK(parameter_kind=6), parameter_id uuid NOT NULL, parameter_version integer NOT NULL", "role", "role,parameter_kind,parameter_id,parameter_version", "j->>'Role',(j->'ParameterSet'->>'Kind')::smallint,(j->'ParameterSet'->>'Id')::uuid,(j->'ParameterSet'->>'Version')::integer", "jsonb_build_object('Role',role,'ParameterSet',jsonb_build_object('Kind',parameter_kind,'Id',parameter_id,'Version',parameter_version))", ", FOREIGN KEY(parameter_kind,parameter_id,parameter_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version)"), new("legacy_trade_strategy_family_mapping", "LegacyFamilies", "owner_kind=7", "legacy_id integer NOT NULL CHECK(legacy_id>0), legacy_version bigint NOT NULL CHECK(legacy_version>0)", "legacy_id,legacy_version", "legacy_id,legacy_version", "(j->>'Id')::integer,(j->>'Version')::bigint", "jsonb_build_object('Id',legacy_id,'Version',legacy_version)")];
        static CatalogChildTable Relation(string name, string property, int owner, int target) => new(name, property, $"owner_kind={owner}", $"target_kind smallint NOT NULL CHECK(target_kind={target}), target_id uuid NOT NULL, target_version integer NOT NULL", "target_kind,target_id,target_version", "target_kind,target_id,target_version", "(j->>'Kind')::smallint,(j->>'Id')::uuid,(j->>'Version')::integer", "jsonb_build_object('Kind',target_kind,'Id',target_id,'Version',target_version)", ", FOREIGN KEY(target_kind,target_id,target_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version)");
        internal static string Create => Header + string.Concat(Children.Select(c => $$"""

CREATE TABLE IF NOT EXISTS reference_configuration.{{c.Name}} (
    owner_kind smallint NOT NULL CHECK({{c.OwnerCheck}}), owner_id uuid NOT NULL, owner_version integer NOT NULL,
    {{c.Columns}}, PRIMARY KEY(owner_kind,owner_id,owner_version,{{c.KeyColumns}}),
    FOREIGN KEY(owner_kind,owner_id,owner_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version)
    {{c.Constraints}}
);
CREATE OR REPLACE TRIGGER guard_{{c.Name}} BEFORE INSERT OR UPDATE OR DELETE ON reference_configuration.{{c.Name}}
FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_strategy_catalog_child();

""")) + SealGuard;
        internal static string Drop => string.Concat(Children.Reverse().Select(c => $"DROP TABLE IF EXISTS {Prefix}{c.Name};\n")) + """
DROP TABLE IF EXISTS reference_configuration.strategy_catalog_version;
DROP TABLE IF EXISTS reference_configuration.strategy_catalog_identity;
DROP FUNCTION IF EXISTS reference_configuration.guard_strategy_catalog_child();
DROP FUNCTION IF EXISTS reference_configuration.guard_strategy_catalog_version();
DROP FUNCTION IF EXISTS reference_configuration.guard_strategy_catalog_identity();
DROP FUNCTION IF EXISTS reference_configuration.require_strategy_catalog_sealed();
""";

        internal const string Header = """
CREATE TABLE IF NOT EXISTS reference_configuration.strategy_catalog_identity (
    kind smallint NOT NULL CHECK(kind BETWEEN 1 AND 7), id uuid NOT NULL CHECK(id<>'00000000-0000-0000-0000-000000000000'),
    code text NOT NULL CHECK(code ~ '^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$'),
    created_utc timestamptz NOT NULL, created_by text NOT NULL CHECK(length(trim(created_by))>0),
    PRIMARY KEY(kind,id), UNIQUE(kind,code)
);
CREATE TABLE IF NOT EXISTS reference_configuration.strategy_catalog_version (
    kind smallint NOT NULL, id uuid NOT NULL, version integer NOT NULL CHECK(version>0),
    schema_version smallint NOT NULL CHECK(schema_version=1), name text NOT NULL CHECK(length(trim(name))>0), description text NOT NULL,
    status smallint NOT NULL DEFAULT 0 CHECK(status IN (0,1,2)), content_sealed boolean NOT NULL DEFAULT false,
    parent_kind smallint NULL, parent_id uuid NULL, parent_version integer NULL,
    horizon smallint NOT NULL DEFAULT 0, side text NOT NULL, bias text NOT NULL, premium_mode text NOT NULL,
    settings_json jsonb NOT NULL CHECK(jsonb_typeof(settings_json)='object'), content_sha256 text NOT NULL CHECK(content_sha256 ~ '^[0-9a-f]{64}$'),
    created_utc timestamptz NOT NULL, created_by text NOT NULL CHECK(length(trim(created_by))>0),
    effective_from_utc timestamptz NULL, published_by text NULL, retired_at_utc timestamptz NULL, retired_by text NULL,
    PRIMARY KEY(kind,id,version), UNIQUE(kind,id,version,parent_kind,parent_id,parent_version),
    FOREIGN KEY(kind,id) REFERENCES reference_configuration.strategy_catalog_identity(kind,id),
    FOREIGN KEY(parent_kind,parent_id,parent_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version) MATCH FULL,
    CHECK(((kind=4 AND parent_kind=3 AND parent_id IS NOT NULL AND parent_version>0)
       OR (kind=6 AND parent_kind=5 AND parent_id IS NOT NULL AND parent_version>0)
       OR (kind=7 AND parent_kind=2 AND parent_id IS NOT NULL AND parent_version>0)
       OR (kind IN (1,2,3,5) AND parent_kind IS NULL AND parent_id IS NULL AND parent_version IS NULL)) IS TRUE),
    CHECK((kind=7 AND horizon IN (1,2,3)) OR (kind<>7 AND horizon=0)),
    CHECK((kind=4 AND side<>'' AND bias<>'' AND premium_mode<>'') OR (kind<>4 AND side='' AND bias='' AND premium_mode='')),
    CHECK(((status=0 AND effective_from_utc IS NULL AND published_by IS NULL AND retired_at_utc IS NULL AND retired_by IS NULL)
       OR (status=1 AND effective_from_utc IS NOT NULL AND length(trim(published_by))>0 AND retired_at_utc IS NULL AND retired_by IS NULL)
       OR (status=2 AND effective_from_utc IS NOT NULL AND length(trim(published_by))>0 AND retired_at_utc>=effective_from_utc AND length(trim(retired_by))>0)) IS TRUE)
);
CREATE INDEX IF NOT EXISTS ix_strategy_catalog_effective ON reference_configuration.strategy_catalog_version(kind,status,horizon,effective_from_utc);

CREATE OR REPLACE FUNCTION reference_configuration.guard_strategy_catalog_identity() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Catalog identities are immutable and cannot be deleted'; END $$;
CREATE OR REPLACE TRIGGER guard_strategy_catalog_identity BEFORE UPDATE OR DELETE ON reference_configuration.strategy_catalog_identity
FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_strategy_catalog_identity();

CREATE OR REPLACE FUNCTION reference_configuration.guard_strategy_catalog_version() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    PERFORM pg_advisory_xact_lock(497003170627);
    IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Catalog versions cannot be deleted'; END IF;
    IF TG_OP='INSERT' THEN
        IF NEW.status<>0 OR NEW.content_sealed THEN RAISE EXCEPTION 'Catalog versions must be inserted as unsealed Draft'; END IF;
        RETURN NEW;
    END IF;
    IF (to_jsonb(NEW)-ARRAY['status','content_sealed','effective_from_utc','published_by','retired_at_utc','retired_by'])
       IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['status','content_sealed','effective_from_utc','published_by','retired_at_utc','retired_by'])
       THEN RAISE EXCEPTION 'Catalog version content is immutable'; END IF;
    IF NOT OLD.content_sealed AND NEW.content_sealed AND NEW.status=0 AND OLD.status=0 THEN RETURN NEW; END IF;
    IF NOT OLD.content_sealed OR NOT NEW.content_sealed THEN RAISE EXCEPTION 'Catalog content must be sealed'; END IF;
    IF OLD.status=0 AND NEW.status=1 THEN RETURN NEW; END IF;
    IF OLD.status=1 AND NEW.status=2 AND NEW.effective_from_utc=OLD.effective_from_utc AND NEW.published_by=OLD.published_by THEN RETURN NEW; END IF;
    RAISE EXCEPTION 'Invalid catalog lifecycle transition';
END $$;
CREATE OR REPLACE TRIGGER guard_strategy_catalog_version BEFORE INSERT OR UPDATE OR DELETE ON reference_configuration.strategy_catalog_version
FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_strategy_catalog_version();

CREATE OR REPLACE FUNCTION reference_configuration.guard_strategy_catalog_child() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE sealed boolean;
BEGIN
    PERFORM pg_advisory_xact_lock(497003170627);
    IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Catalog child content is immutable'; END IF;
    SELECT content_sealed INTO sealed FROM reference_configuration.strategy_catalog_version
      WHERE kind=NEW.owner_kind AND id=NEW.owner_id AND version=NEW.owner_version FOR UPDATE;
    IF sealed IS DISTINCT FROM false THEN RAISE EXCEPTION 'Catalog child requires an unsealed owning draft'; END IF;
    RETURN NEW;
END $$;

""";
        const string SealGuard = """
CREATE OR REPLACE FUNCTION reference_configuration.require_strategy_catalog_sealed() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM reference_configuration.strategy_catalog_version WHERE kind=NEW.kind AND id=NEW.id AND version=NEW.version AND content_sealed)
      THEN RAISE EXCEPTION 'Incomplete unsealed catalog draft cannot commit'; END IF;
    RETURN NULL;
END $$;
DO $$ BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_trigger WHERE tgname='require_strategy_catalog_sealed' AND tgrelid='reference_configuration.strategy_catalog_version'::regclass) THEN
        CREATE CONSTRAINT TRIGGER require_strategy_catalog_sealed AFTER INSERT ON reference_configuration.strategy_catalog_version
        DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION reference_configuration.require_strategy_catalog_sealed();
    END IF;
END $$;
""";
    }

    /// <summary>Defines PostgreSQL volatility series schema objects.</summary>
    public static class VolatilitySeries
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

    /// <summary>Defines PostgreSQL parameter registry schema objects.</summary>
    public static class ParameterRegistry
    {
        public static string Create => Tables + string.Join("\n", ParameterSchemaRegistry.Default.Definitions.Select(schema => $"INSERT INTO reference_configuration.parameter_schema_version(component_code,schema_version,codec,schema_sha256,schema_json) VALUES ('{schema.ComponentCode.Replace("'", "''", System.StringComparison.Ordinal)}',{schema.Version},'{schema.Codec}','{schema.SchemaSha256}','{schema.JsonSchema.Replace("'", "''", System.StringComparison.Ordinal)}'::jsonb) ON CONFLICT(component_code,schema_version) DO NOTHING;"));

        const string Tables = """
 CREATE TABLE IF NOT EXISTS reference_configuration.parameter_area (
 area_id uuid PRIMARY KEY,code text NOT NULL UNIQUE,name text NOT NULL,enabled boolean NOT NULL DEFAULT true);
 CREATE TABLE IF NOT EXISTS reference_configuration.parameter_component (
 component_id uuid PRIMARY KEY,code text NOT NULL UNIQUE,area_id uuid NOT NULL REFERENCES reference_configuration.parameter_area(area_id),
 name text NOT NULL,editor_code text NOT NULL,enabled boolean NOT NULL DEFAULT true);
 CREATE TABLE IF NOT EXISTS reference_configuration.parameter_schema_version (
 component_code text NOT NULL REFERENCES reference_configuration.parameter_component(code),schema_version integer NOT NULL CHECK(schema_version>0),
 codec text NOT NULL,schema_sha256 text NOT NULL CHECK(schema_sha256 ~ '^[0-9a-f]{64}$'),schema_json jsonb NOT NULL CHECK(jsonb_typeof(schema_json)='object'),
 PRIMARY KEY(component_code,schema_version));
 INSERT INTO reference_configuration.parameter_area(area_id,code,name)
 VALUES(md5('parameter-area:strategy-workflow')::uuid,'strategy-workflow','Strategy Workflow') ON CONFLICT DO NOTHING;
 INSERT INTO reference_configuration.parameter_area(area_id,code,name)
 VALUES(md5('parameter-area:market-data-analytics')::uuid,'market-data-analytics','Market Data Analytics') ON CONFLICT DO NOTHING;
 INSERT INTO reference_configuration.parameter_area(area_id,code,name)
 VALUES(md5('parameter-area:option-volatility')::uuid,'option-volatility','Option Volatility') ON CONFLICT DO NOTHING;
 INSERT INTO reference_configuration.parameter_area(area_id,code,name)
 VALUES(md5('parameter-area:option-spread-strategy')::uuid,'option-spread-strategy','Option Spread Strategy Defaults') ON CONFLICT DO NOTHING;
 INSERT INTO reference_configuration.parameter_component(component_id,code,area_id,name,editor_code)
 VALUES(md5('parameter-component:strategy-workflow.regime-discovery')::uuid,'strategy-workflow.regime-discovery',md5('parameter-area:strategy-workflow')::uuid,'Regime Discovery','regime-discovery') ON CONFLICT DO NOTHING;
 INSERT INTO reference_configuration.parameter_component(component_id,code,area_id,name,editor_code)
 VALUES(md5('parameter-component:market-data-analytics.futures-iti-signal')::uuid,'market-data-analytics.futures-iti-signal',md5('parameter-area:market-data-analytics')::uuid,'Future ITI Signal','scalar-properties') ON CONFLICT DO NOTHING;
 INSERT INTO reference_configuration.parameter_component(component_id,code,area_id,name,editor_code)
 VALUES(md5('parameter-component:option-volatility.series')::uuid,'option-volatility.series',md5('parameter-area:option-volatility')::uuid,'Series','scalar-properties') ON CONFLICT DO NOTHING;
 INSERT INTO reference_configuration.parameter_component(component_id,code,area_id,name,editor_code)
 VALUES(md5('parameter-component:option-volatility.consumer-rules')::uuid,'option-volatility.consumer-rules',md5('parameter-area:option-volatility')::uuid,'Consumer Rules','scalar-properties') ON CONFLICT DO NOTHING;
 INSERT INTO reference_configuration.parameter_component(component_id,code,area_id,name,editor_code)
 VALUES(md5('parameter-component:option-volatility.retention')::uuid,'option-volatility.retention',md5('parameter-area:option-volatility')::uuid,'Retention','scalar-properties') ON CONFLICT DO NOTHING;
 INSERT INTO reference_configuration.parameter_component(component_id,code,area_id,name,editor_code)
 VALUES(md5('parameter-component:option-spread-strategy.iron-condor-defaults')::uuid,'option-spread-strategy.iron-condor-defaults',md5('parameter-area:option-spread-strategy')::uuid,'Iron Condor','scalar-properties') ON CONFLICT DO NOTHING;
 INSERT INTO reference_configuration.parameter_component(component_id,code,area_id,name,editor_code)
 VALUES(md5('parameter-component:option-spread-strategy.vertical-spread-defaults')::uuid,'option-spread-strategy.vertical-spread-defaults',md5('parameter-area:option-spread-strategy')::uuid,'Vertical Spreads','scalar-properties') ON CONFLICT DO NOTHING;
 INSERT INTO reference_configuration.parameter_area(area_id,code,name)
 VALUES(md5('parameter-area:market-data')::uuid,'market-data','Market Data') ON CONFLICT DO NOTHING;
 INSERT INTO reference_configuration.parameter_component(component_id,code,area_id,name,editor_code)
 VALUES(md5('parameter-component:market-data.strategy-option-chain-cache')::uuid,'market-data.strategy-option-chain-cache',md5('parameter-area:market-data')::uuid,'Strategy Option Chain Cache','scalar-properties') ON CONFLICT DO NOTHING;
 CREATE OR REPLACE FUNCTION reference_configuration.guard_parameter_schema() RETURNS trigger LANGUAGE plpgsql AS $$
 BEGIN RAISE EXCEPTION 'PARAM.SCHEMA_IMMUTABLE'; END; $$;
 DROP TRIGGER IF EXISTS parameter_schema_guard ON reference_configuration.parameter_schema_version;
 CREATE TRIGGER parameter_schema_guard BEFORE UPDATE OR DELETE ON reference_configuration.parameter_schema_version
 FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_parameter_schema();
 """;
    }

    /// <summary>Defines PostgreSQL parameter sets schema objects.</summary>
    public static class ParameterSets
    {
        public const string Create = """
 CREATE TABLE IF NOT EXISTS reference_configuration.parameter_legacy_reference (
 legacy_kind text NOT NULL,legacy_set_id uuid NOT NULL,legacy_version integer NOT NULL,generic_set_id uuid NOT NULL,generic_version integer NOT NULL,
 legacy_sha256 text NOT NULL,legacy_codec text NOT NULL,PRIMARY KEY(legacy_kind,legacy_set_id,legacy_version));
 CREATE TABLE IF NOT EXISTS reference_configuration.parameter_startup_report (
 run_id uuid NOT NULL,revision bigint NOT NULL CHECK(revision>0),body jsonb NOT NULL CHECK(jsonb_typeof(body)='object'),PRIMARY KEY(run_id,revision));
 CREATE TABLE IF NOT EXISTS reference_configuration.parameter_startup_run (
 run_id uuid PRIMARY KEY,body jsonb NOT NULL CHECK(jsonb_typeof(body)='object'),active boolean NOT NULL DEFAULT true);
 CREATE TABLE IF NOT EXISTS reference_configuration.parameter_set_audit (
 operation_id uuid PRIMARY KEY, entity_id uuid NOT NULL, revision bigint NOT NULL CHECK(revision>0),
 body jsonb NOT NULL CHECK(jsonb_typeof(body)='object'), UNIQUE(entity_id,revision));
 CREATE TABLE IF NOT EXISTS reference_configuration.parameter_set (
 set_id uuid PRIMARY KEY, component_code text NOT NULL, name text NOT NULL, description text NOT NULL,
 revision bigint NOT NULL CHECK(revision>0));
 CREATE TABLE IF NOT EXISTS reference_configuration.parameter_set_version (
 set_id uuid NOT NULL REFERENCES reference_configuration.parameter_set(set_id), version integer NOT NULL CHECK(version>0),
 payload_sha256 text NOT NULL CHECK(length(payload_sha256)=64), status smallint NOT NULL CHECK(status IN(0,1,2)),
 body jsonb NOT NULL CHECK(jsonb_typeof(body)='object'), PRIMARY KEY(set_id,version));
 CREATE TABLE IF NOT EXISTS reference_configuration.parameter_operation (
 operation_id uuid PRIMARY KEY, set_id uuid NOT NULL REFERENCES reference_configuration.parameter_set(set_id),
 revision bigint NOT NULL, request_sha256 text NOT NULL, version integer NOT NULL,
 committed_at_utc timestamptz NOT NULL DEFAULT now(), UNIQUE(set_id,revision));
 CREATE TABLE IF NOT EXISTS reference_configuration.parameter_assignment (
 assignment_id uuid PRIMARY KEY, revision bigint NOT NULL CHECK(revision>0), body jsonb NOT NULL CHECK(jsonb_typeof(body)='object'));
 CREATE TABLE IF NOT EXISTS reference_configuration.parameter_assignment_revision (
 assignment_id uuid NOT NULL REFERENCES reference_configuration.parameter_assignment(assignment_id), revision bigint NOT NULL CHECK(revision>0),
 operation_id uuid NOT NULL UNIQUE, request_sha256 text NOT NULL CHECK(request_sha256 ~ '^[0-9a-f]{64}$'),
 body jsonb NOT NULL CHECK(jsonb_typeof(body)='object'), PRIMARY KEY(assignment_id,revision));
 CREATE OR REPLACE FUNCTION reference_configuration.guard_parameter_version() RETURNS trigger
 LANGUAGE plpgsql AS $$
 BEGIN
   IF TG_OP = 'DELETE' THEN
     RAISE EXCEPTION 'PARAM.VERSION_IMMUTABLE';
   END IF;
   IF TG_OP = 'UPDATE' THEN
     IF NEW.set_id <> OLD.set_id OR NEW.version <> OLD.version
       OR NEW.payload_sha256 <> OLD.payload_sha256
       OR NEW.body->'PayloadJson' IS DISTINCT FROM OLD.body->'PayloadJson'
       OR NEW.body->'SchemaVersion' IS DISTINCT FROM OLD.body->'SchemaVersion'
       OR COALESCE(NEW.body->'LegacySource','null'::jsonb) IS DISTINCT FROM COALESCE(OLD.body->'LegacySource','null'::jsonb)
       OR NEW.body->'Reference' IS DISTINCT FROM OLD.body->'Reference'
       OR NEW.body->'CreatedAtUtc' IS DISTINCT FROM OLD.body->'CreatedAtUtc'
       OR NEW.body->'CreatedBy' IS DISTINCT FROM OLD.body->'CreatedBy' THEN
       RAISE EXCEPTION 'PARAM.VERSION_IMMUTABLE';
     END IF;
     IF NEW.status <> OLD.status AND NOT ((OLD.status=0 AND NEW.status=1) OR (OLD.status=1 AND NEW.status=2)) THEN
       RAISE EXCEPTION 'PARAM.LIFECYCLE_INVALID';
     END IF;
   END IF;
   IF NEW.payload_sha256 !~ '^[0-9a-f]{64}$'
     OR (NEW.body->'Reference'->>'SetId') IS DISTINCT FROM NEW.set_id::text
     OR (NEW.body->'Reference'->>'Version')::integer IS DISTINCT FROM NEW.version
     OR (NEW.body->'Reference'->>'PayloadSha256') IS DISTINCT FROM NEW.payload_sha256
     OR (NEW.body->>'Status')::integer IS DISTINCT FROM NEW.status::integer THEN
     RAISE EXCEPTION 'PARAM.VERSION_ENVELOPE_INVALID';
   END IF;
   IF NOT EXISTS (SELECT 1 FROM reference_configuration.parameter_set p
      JOIN reference_configuration.parameter_schema_version s ON s.component_code=p.component_code
      WHERE p.set_id=NEW.set_id AND s.schema_version=(NEW.body->>'SchemaVersion')::integer
       AND p.component_code=NEW.body->'Reference'->>'ComponentCode') THEN
      RAISE EXCEPTION 'PARAM.SCHEMA_UNREGISTERED';
   END IF;
   RETURN NEW;
 END;
 $$;
 DROP TRIGGER IF EXISTS parameter_version_guard ON reference_configuration.parameter_set_version;
 CREATE TRIGGER parameter_version_guard BEFORE INSERT OR UPDATE OR DELETE ON reference_configuration.parameter_set_version
 FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_parameter_version();
 CREATE OR REPLACE FUNCTION reference_configuration.guard_parameter_startup() RETURNS trigger LANGUAGE plpgsql AS $$
 BEGIN
  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'PARAM.STARTUP_IMMUTABLE'; END IF;
  IF NEW.run_id IS DISTINCT FROM OLD.run_id OR NEW.body IS DISTINCT FROM OLD.body OR (NOT OLD.active AND NEW.active) THEN
   RAISE EXCEPTION 'PARAM.STARTUP_IMMUTABLE'; END IF;
  RETURN NEW;
 END; $$;
 DROP TRIGGER IF EXISTS parameter_startup_guard ON reference_configuration.parameter_startup_run;
 CREATE TRIGGER parameter_startup_guard BEFORE UPDATE OR DELETE ON reference_configuration.parameter_startup_run
 FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_parameter_startup();
 CREATE OR REPLACE FUNCTION reference_configuration.guard_parameter_history() RETURNS trigger LANGUAGE plpgsql AS $$
 BEGIN RAISE EXCEPTION 'PARAM.HISTORY_IMMUTABLE'; END; $$;
 DROP TRIGGER IF EXISTS parameter_audit_guard ON reference_configuration.parameter_set_audit;
 CREATE TRIGGER parameter_audit_guard BEFORE UPDATE OR DELETE ON reference_configuration.parameter_set_audit
 FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_parameter_history();
 DROP TRIGGER IF EXISTS parameter_assignment_revision_guard ON reference_configuration.parameter_assignment_revision;
 CREATE TRIGGER parameter_assignment_revision_guard BEFORE UPDATE OR DELETE ON reference_configuration.parameter_assignment_revision
 FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_parameter_history();
 DROP TRIGGER IF EXISTS parameter_startup_report_guard ON reference_configuration.parameter_startup_report;
 CREATE TRIGGER parameter_startup_report_guard BEFORE UPDATE OR DELETE ON reference_configuration.parameter_startup_report
 FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_parameter_history();
 DROP TRIGGER IF EXISTS parameter_legacy_guard ON reference_configuration.parameter_legacy_reference;
 CREATE TRIGGER parameter_legacy_guard BEFORE UPDATE OR DELETE ON reference_configuration.parameter_legacy_reference
 FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_parameter_history();
 CREATE INDEX IF NOT EXISTS ix_parameter_set_component ON reference_configuration.parameter_set(component_code,name,set_id);
 """;
    }

    /// <summary>Creates the completion receipt for a configuration baseline.</summary>
    internal const string CreateBaselineCompletion = "CREATE TABLE IF NOT EXISTS reference_configuration.configuration_baseline_application (baseline_id uuid PRIMARY KEY,source_event_id bigint NOT NULL UNIQUE,source_cut bigint NOT NULL);";
    static readonly string[] Tables = ["intrinsic_time_strategy_workflow_parameter_set", "regime_discovery_parameter_set", "market_condition_parameter_set", "trade_selection_parameter_set", "order_composition_parameter_set", "risk_management_parameter_set", "strategy_position_risk_parameter_set"];
    static readonly SchemaObjectDefinition[] Objects = new[]
    {
        new SchemaObjectDefinition("reference_configuration", ConfigurationSchemaSql.CreateSchema, "DROP SCHEMA IF EXISTS reference_configuration CASCADE;")
    }.Concat(Tables.Select(table => new SchemaObjectDefinition(table, ConfigurationSchemaSql.CreateTable(table), $"DROP TABLE IF EXISTS reference_configuration.{table};")))
        .Concat(new[] { "trade_selection_parameter_set", "intrinsic_time_strategy_workflow_parameter_set", "order_composition_parameter_set", "strategy_position_risk_parameter_set" }.Select(table => new SchemaObjectDefinition(table + "_lifecycle_guard", (ConfigurationSchemaSql.EnsureMarketConditionLifecycleConstraints + ConfigurationSchemaSql.CreateMarketConditionLifecycleGuard).Replace("market_condition_parameter_set", table, System.StringComparison.Ordinal).Replace("MarketCondition", table, StringComparison.Ordinal), $"DROP TRIGGER IF EXISTS trg_guard_{table} ON reference_configuration.{table}; DROP FUNCTION IF EXISTS reference_configuration.guard_{table}();")))
        .Append(new SchemaObjectDefinition("ix_market_condition_parameter_set_effective", ConfigurationSchemaSql.CreateMarketConditionEffectiveIndex, "DROP INDEX IF EXISTS reference_configuration.ix_market_condition_parameter_set_effective;"))
        .Append(new SchemaObjectDefinition("market_condition_parameter_set_lifecycle_constraints", ConfigurationSchemaSql.EnsureMarketConditionLifecycleConstraints, """
            ALTER TABLE IF EXISTS reference_configuration.market_condition_parameter_set
            DROP CONSTRAINT IF EXISTS ck_market_condition_parameter_set_lifecycle,
            DROP CONSTRAINT IF EXISTS ck_market_condition_parameter_set_status;
            """))
        .Append(new SchemaObjectDefinition("market_condition_parameter_set_lifecycle_guard", ConfigurationSchemaSql.CreateMarketConditionLifecycleGuard, """
            DROP TRIGGER IF EXISTS trg_guard_market_condition_parameter_set
            ON reference_configuration.market_condition_parameter_set;
            DROP FUNCTION IF EXISTS reference_configuration.guard_market_condition_parameter_set();
            """))
        .Append(new SchemaObjectDefinition("market_condition_assessment_parameter_set", ConfigurationSchemaSql.MarketConditionAssessment.Create, "DROP TABLE IF EXISTS reference_configuration.market_condition_assessment_parameter_set;"))
        .Append(new SchemaObjectDefinition("volatility_series_definition", ConfigurationSchemaSql.VolatilitySeries.Create, ConfigurationSchemaSql.VolatilitySeries.Drop))
        .Append(new SchemaObjectDefinition("lookup_definition", ConfigurationSchemaSql.Lookups.Create, ConfigurationSchemaSql.Lookups.Drop))
        .Append(new SchemaObjectDefinition("strategy_catalog", ConfigurationSchemaSql.StrategyCatalog.Create, ConfigurationSchemaSql.StrategyCatalog.Drop))
        .Append(new SchemaObjectDefinition("parameter_registry", ConfigurationSchemaSql.ParameterRegistry.Create, "DROP TABLE IF EXISTS reference_configuration.parameter_schema_version; DROP TABLE IF EXISTS reference_configuration.parameter_component; DROP TABLE IF EXISTS reference_configuration.parameter_area;"))
        .Append(new SchemaObjectDefinition("parameter_sets", ConfigurationSchemaSql.ParameterSets.Create, "DROP TABLE IF EXISTS reference_configuration.parameter_assignment_revision; DROP TABLE IF EXISTS reference_configuration.parameter_assignment; DROP TABLE IF EXISTS reference_configuration.parameter_operation; DROP TABLE IF EXISTS reference_configuration.parameter_set_version; DROP TABLE IF EXISTS reference_configuration.parameter_set;"))
        .Concat(TypedParameters.Definitions)
        .Concat(TypedCatalog.Definitions)
        .Append(new SchemaObjectDefinition("configuration_baseline_application", ConfigurationSchemaSql.CreateBaselineCompletion, "DROP TABLE IF EXISTS reference_configuration.configuration_baseline_application;"))
        .ToArray();
    /// <summary>Gets all ConfigurationDb schema objects in dependency order.</summary>
    /// <param name = "seedLookupDefaults">Whether lookup defaults are included in schema initialization.</param>
    /// <returns>The complete create/drop catalog, including tables, indexes, constraints, functions and triggers.</returns>
    public static SchemaObjectDefinition[] GetDefinitions(bool seedLookupDefaults = true) => seedLookupDefaults ? [..Objects] : Objects.Select(definition => string.Equals(definition.Name, "lookup_definition", StringComparison.Ordinal) ? definition with { CreateStatement = Lookups.CreateStructure } : definition)
        .ToArray();
}

internal sealed record CatalogChildTable(string Name, string Property, string OwnerCheck, string Columns, string KeyColumns, string InsertColumns, string InsertValues, string ReadJson, string Constraints = "");
