using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;

internal sealed record CatalogChildTable(string Name, string Property, string OwnerCheck, string Columns,
    string KeyColumns, string InsertColumns, string InsertValues, string ReadJson, string Constraints = "");

/// <summary>Normalized, additive catalog schema. SQL identifiers come exclusively from this static catalog.</summary>
internal static class StrategyCatalogSchemaSql
{
    internal const long WriterLock = 497003170627;
    internal const string Prefix = "reference_configuration.";
    internal static readonly CatalogChildTable[] Children =
    [
        Relation("strategy_family_membership", "Families", 2, 1),
        Relation("strategy_structure_assignment", "Structures", 2, 3),
        Relation("strategy_deployment_structure_variant", "Variants", 7, 4),
        new("strategy_capability_requirement", "Capabilities", "owner_kind <> 1",
            "role text NOT NULL CHECK(role IN ('evaluator','builder','validator','risk','data')), code text NOT NULL, capability_version integer NOT NULL CHECK(capability_version>0)",
            "role,code,capability_version", "role,code,capability_version", "j->>'Role',j->>'Code',(j->>'Version')::integer",
            "jsonb_build_object('Role',role,'Code',code,'Version',capability_version)"),
        new("structure_expiry_group", "ExpiryGroups", "owner_kind=3",
            "group_key text NOT NULL, after_group text NULL", "group_key", "group_key,after_group", "j->>'Key',j->>'AfterGroup'",
            "jsonb_build_object('Key',group_key,'AfterGroup',after_group)",
            ", FOREIGN KEY(owner_kind,owner_id,owner_version,after_group) REFERENCES reference_configuration.structure_expiry_group(owner_kind,owner_id,owner_version,group_key) DEFERRABLE INITIALLY DEFERRED"),
        new("structure_leg_definition", "Legs", "owner_kind=3",
            "leg_key text NOT NULL, instrument_class text NOT NULL CHECK(instrument_class IN ('Futures','FuturesOption')), side text NOT NULL CHECK(side IN ('Buy','Sell')), option_right text NOT NULL, ratio numeric NOT NULL CHECK(ratio>0 AND ratio<=1000000), expiry_group text NOT NULL",
            "leg_key", "leg_key,instrument_class,side,option_right,ratio,expiry_group", "j->>'Key',j->>'InstrumentClass',j->>'Side',j->>'OptionRight',(j->>'Ratio')::numeric,j->>'ExpiryGroup'",
            "jsonb_build_object('Key',leg_key,'InstrumentClass',instrument_class,'Side',side,'OptionRight',option_right,'Ratio',ratio,'ExpiryGroup',expiry_group)",
            ", CHECK((instrument_class='Futures' AND option_right='None') OR (instrument_class='FuturesOption' AND option_right IN ('Call','Put'))), FOREIGN KEY(owner_kind,owner_id,owner_version,expiry_group) REFERENCES reference_configuration.structure_expiry_group(owner_kind,owner_id,owner_version,group_key)"),
        new("structure_variant_leg_rule", "VariantLegs", "owner_kind=4",
            "leg_key text NOT NULL, side text NOT NULL CHECK(side IN ('Buy','Sell')), ratio numeric NOT NULL CHECK(ratio>0 AND ratio<=1000000), structure_kind smallint NOT NULL CHECK(structure_kind=3), structure_id uuid NOT NULL, structure_version integer NOT NULL",
            "leg_key", "leg_key,side,ratio,structure_kind,structure_id,structure_version",
            "j->>'LegKey',j->>'Side',(j->>'Ratio')::numeric,3,(d->'Parent'->>'Id')::uuid,(d->'Parent'->>'Version')::integer",
            "jsonb_build_object('LegKey',leg_key,'Side',side,'Ratio',ratio)",
            ", FOREIGN KEY(structure_kind,structure_id,structure_version,leg_key) REFERENCES reference_configuration.structure_leg_definition(owner_kind,owner_id,owner_version,leg_key), FOREIGN KEY(owner_kind,owner_id,owner_version,structure_kind,structure_id,structure_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version,parent_kind,parent_id,parent_version)"),
        new("strategy_deployment_product", "Products", "owner_kind=7",
            "product_id integer NOT NULL CHECK(product_id>0), symbol text NOT NULL, exchange text NOT NULL, currency text NOT NULL",
            "product_id", "product_id,symbol,exchange,currency", "(j->>'ProductId')::integer,j->>'Symbol',j->>'Exchange',j->>'Currency'",
            "jsonb_build_object('ProductId',product_id,'Symbol',symbol,'Exchange',exchange,'Currency',currency)"),
        new("strategy_deployment_parameter_binding", "PipelineParameters", "owner_kind=7",
            "role text NOT NULL, parameter_kind smallint NOT NULL CHECK(parameter_kind BETWEEN 1 AND 7), parameter_id uuid NOT NULL, parameter_version integer NOT NULL CHECK(parameter_version>0), payload_sha256 text NOT NULL CHECK(payload_sha256 ~ '^[0-9a-f]{64}$')",
            "role", "role,parameter_kind,parameter_id,parameter_version,payload_sha256", "j->>'Role',(j->>'Kind')::smallint,(j->>'Id')::uuid,(j->>'Version')::integer,j->>'Hash'",
            "jsonb_build_object('Role',role,'Kind',parameter_kind,'Id',parameter_id,'Version',parameter_version,'Hash',payload_sha256)"),
        new("strategy_deployment_catalog_parameter_binding", "Parameters", "owner_kind=7",
            "role text NOT NULL, parameter_kind smallint NOT NULL CHECK(parameter_kind=6), parameter_id uuid NOT NULL, parameter_version integer NOT NULL",
            "role", "role,parameter_kind,parameter_id,parameter_version", "j->>'Role',(j->'ParameterSet'->>'Kind')::smallint,(j->'ParameterSet'->>'Id')::uuid,(j->'ParameterSet'->>'Version')::integer",
            "jsonb_build_object('Role',role,'ParameterSet',jsonb_build_object('Kind',parameter_kind,'Id',parameter_id,'Version',parameter_version))",
            ", FOREIGN KEY(parameter_kind,parameter_id,parameter_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version)"),
        new("legacy_trade_strategy_family_mapping", "LegacyFamilies", "owner_kind=7",
            "legacy_id integer NOT NULL CHECK(legacy_id>0), legacy_version bigint NOT NULL CHECK(legacy_version>0)",
            "legacy_id,legacy_version", "legacy_id,legacy_version", "(j->>'Id')::integer,(j->>'Version')::bigint",
            "jsonb_build_object('Id',legacy_id,'Version',legacy_version)")
    ];

    static CatalogChildTable Relation(string name, string property, int owner, int target) => new(name, property,
        $"owner_kind={owner}", $"target_kind smallint NOT NULL CHECK(target_kind={target}), target_id uuid NOT NULL, target_version integer NOT NULL",
        "target_kind,target_id,target_version", "target_kind,target_id,target_version", "(j->>'Kind')::smallint,(j->>'Id')::uuid,(j->>'Version')::integer",
        "jsonb_build_object('Kind',target_kind,'Id',target_id,'Version',target_version)",
        ", FOREIGN KEY(target_kind,target_id,target_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version)");

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

    internal static string DefinitionJson => """
jsonb_build_object('Key',jsonb_build_object('Kind',v.kind,'Id',v.id,'Version',v.version),
'Code',i.code,'Name',v.name,'Description',v.description,'SchemaVersion',v.schema_version,
'Parent',CASE WHEN v.parent_id IS NULL THEN NULL ELSE jsonb_build_object('Kind',v.parent_kind,'Id',v.parent_id,'Version',v.parent_version) END,
'Horizon',v.horizon,'Side',v.side,'Bias',v.bias,'PremiumMode',v.premium_mode,'Settings',v.settings_json)
""" + string.Concat(Children.Select(c => $" || jsonb_build_object('{c.Property}',COALESCE((SELECT jsonb_agg({c.ReadJson}) FROM {Prefix}{c.Name} c WHERE c.owner_kind=v.kind AND c.owner_id=v.id AND c.owner_version=v.version),'[]'::jsonb))"));

    internal static string Exact => $"SELECT ({DefinitionJson})::text,v.content_sha256,v.status,v.created_utc,v.created_by,v.effective_from_utc,v.published_by,v.retired_at_utc,v.retired_by FROM {Prefix}strategy_catalog_version v JOIN {Prefix}strategy_catalog_identity i USING(kind,id) WHERE v.kind=$1 AND v.id=$2 AND v.version=$3";

    internal static string InsertChildren(CatalogChildTable c) => $"""
WITH input AS (SELECT $1::jsonb AS d)
INSERT INTO {Prefix}{c.Name}(owner_kind,owner_id,owner_version,{c.InsertColumns})
SELECT (d->'Key'->>'Kind')::smallint,(d->'Key'->>'Id')::uuid,(d->'Key'->>'Version')::integer,{c.InsertValues}
FROM input CROSS JOIN LATERAL jsonb_array_elements(d->'{c.Property}') j;
""";
}
