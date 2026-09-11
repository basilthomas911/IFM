namespace TomasAI.IFM.Application.Storage.ConfigurationDb.ParameterSets;
public static class ParameterSetSchemaSql
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
