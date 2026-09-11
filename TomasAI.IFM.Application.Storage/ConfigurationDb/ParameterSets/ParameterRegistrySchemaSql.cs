using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Application.Storage.ConfigurationDb.ParameterSets;
public static class ParameterRegistrySchemaSql
{
 public static string Create => Tables + string.Join("\n",ParameterSchemaRegistry.Default.Definitions.Select(schema =>
  $"INSERT INTO reference_configuration.parameter_schema_version(component_code,schema_version,codec,schema_sha256,schema_json) VALUES ('{schema.ComponentCode.Replace("'","''")}',{schema.Version},'{schema.Codec}','{schema.SchemaSha256}','{schema.JsonSchema.Replace("'","''")}'::jsonb) ON CONFLICT(component_code,schema_version) DO NOTHING;"));
 const string Tables="""
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
 INSERT INTO reference_configuration.parameter_component(component_id,code,area_id,name,editor_code)
 VALUES(md5('parameter-component:strategy-workflow.regime-discovery')::uuid,'strategy-workflow.regime-discovery',md5('parameter-area:strategy-workflow')::uuid,'Regime Discovery','regime-discovery') ON CONFLICT DO NOTHING;
 CREATE OR REPLACE FUNCTION reference_configuration.guard_parameter_schema() RETURNS trigger LANGUAGE plpgsql AS $$
 BEGIN RAISE EXCEPTION 'PARAM.SCHEMA_IMMUTABLE'; END; $$;
 DROP TRIGGER IF EXISTS parameter_schema_guard ON reference_configuration.parameter_schema_version;
 CREATE TRIGGER parameter_schema_guard BEFORE UPDATE OR DELETE ON reference_configuration.parameter_schema_version
 FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_parameter_schema();
 """;
}
