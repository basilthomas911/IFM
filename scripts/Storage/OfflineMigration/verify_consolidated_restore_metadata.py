"""Compare schema definitions and sequence state in the exact local restore fixtures."""
import argparse
import json
import re
import subprocess
from pathlib import Path

SOURCE = "ifm_storage_migration_staging_consolidated_20261012"
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--verify-development-clone", action="store_true", help="Read-only verification of the exact prepared Development clone, before any application writer starts")
parser.add_argument("--verify-final-development-restore", action="store_true", help="Verify the final Development archive against its dedicated isolated restore")
options = parser.parse_args()
RESTORED = "ifm_application_development" if options.verify_development_clone else "ifm_storage_migration_staging_restored_20261012"
PROOF_NAME = "development-clone-metadata-proof-20261012.json" if options.verify_development_clone else "consolidated-isolated-restore-metadata-proof-20261012.json"
if options.verify_final_development_restore:
    if options.verify_development_clone:
        parser.error("Select only one verification mode")
    SOURCE = "ifm_application_development"
    RESTORED = "ifm_storage_final_restore_20261012"
    PROOF_NAME = "final-development-restore-metadata-proof-20261012.json"
ARTIFACTS = Path(__file__).resolve().parents[3] / ".artifacts" / "storage-cutover"

def query(database, sql):
    if database not in (SOURCE, RESTORED):
        raise ValueError("Only owned isolated databases are accepted")
    return subprocess.run(["docker", "exec", "-i", "ifm_db", "psql", "-X", "-U", "postgres", "-d", database, "-At", "-v", "ON_ERROR_STOP=1"], input=sql, text=True, capture_output=True, check=True).stdout.strip()

def identifier(value):
    if not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", value):
        raise ValueError("Unexpected metadata identifier")
    return '"' + value + '"'

metadata = {
    "columns": "SELECT COALESCE(json_agg(row_to_json(t) ORDER BY table_schema,table_name,ordinal_position),'[]') FROM (SELECT table_schema,table_name,column_name,ordinal_position,is_nullable,data_type,udt_schema,udt_name,column_default FROM information_schema.columns WHERE table_schema NOT IN ('pg_catalog','information_schema')) t;",
    "constraints": "SELECT COALESCE(json_agg(row_to_json(t) ORDER BY schema_name,table_name,name),'[]') FROM (SELECT n.nspname AS schema_name,c.relname AS table_name,k.conname AS name,pg_get_constraintdef(k.oid) AS definition,k.convalidated AS validated FROM pg_constraint k JOIN pg_class c ON c.oid=k.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname NOT IN ('pg_catalog','information_schema')) t;",
    "indexes": "SELECT COALESCE(json_agg(row_to_json(t) ORDER BY schemaname,tablename,indexname),'[]') FROM (SELECT schemaname,tablename,indexname,indexdef FROM pg_indexes WHERE schemaname NOT IN ('pg_catalog','information_schema')) t;",
    "sequences": "SELECT COALESCE(json_agg(row_to_json(t) ORDER BY sequence_schema,sequence_name),'[]') FROM (SELECT sequence_schema,sequence_name,data_type,numeric_precision,numeric_scale,start_value,minimum_value,maximum_value,increment,cycle_option FROM information_schema.sequences WHERE sequence_schema NOT IN ('pg_catalog','information_schema')) t;"
}
counts = {}
for name, sql in metadata.items():
    before, actual, after = [json.loads(query(db,sql)) for db in (SOURCE,RESTORED,SOURCE)]
    if name == "indexes":
        # pg_dump/restore reparses an array-wide varchar-to-text cast as identical per-element casts.
        # Accept only this exact two-literal deparser form; all index names, keys and other predicates remain exact.
        pattern = re.compile(r"\(ARRAY\['([^']*)'::character varying, '([^']*)'::character varying\]\)::text\[\]")
        for rows in (before,actual,after):
            for row in rows:
                row['indexdef'] = pattern.sub(lambda m: "ARRAY[('" + m[1] + "'::character varying)::text, ('" + m[2] + "'::character varying)::text]",row['indexdef'])
    if before != actual or before != after:
        (ARTIFACTS / ("restore-metadata-mismatch-" + name + ".json")).write_text(json.dumps(dict(source=before,restored=actual,after=after),indent=2),encoding="utf-8")
        raise RuntimeError("Restore metadata mismatch: " + name)
    counts[name] = len(before)
sequence_names = json.loads(query(SOURCE,"SELECT COALESCE(json_agg(json_build_object('schema',sequence_schema,'name',sequence_name) ORDER BY sequence_schema,sequence_name),'[]') FROM information_schema.sequences;"))
for item in sequence_names:
    sql = "SELECT json_build_object('lastValue',last_value,'called',is_called) FROM " + identifier(item['schema']) + '.' + identifier(item['name']) + ';'
    values = [json.loads(query(db,sql)) for db in (SOURCE,RESTORED,SOURCE)]
    if values[0] != values[1] or values[0] != values[2]:
        raise RuntimeError("Restored sequence state mismatch: " + item['name'])
proof = dict(sourceDatabase=SOURCE,restoredDatabase=RESTORED,metadataCounts=counts,sequenceStatesVerified=len(sequence_names),sourceUnchanged=True)
(ARTIFACTS / PROOF_NAME).write_text(json.dumps(proof,indent=2),encoding='utf-8')
print(json.dumps(proof))
