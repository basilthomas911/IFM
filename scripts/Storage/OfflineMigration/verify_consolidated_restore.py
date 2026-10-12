"""Read-only complete-row verification of the owned local consolidated restore fixture."""
from pathlib import Path
import hashlib
import argparse
import json
import re
import subprocess

CONTAINER = "ifm_db"
SOURCE = "ifm_storage_migration_staging_consolidated_20261012"
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--verify-development-clone", action="store_true", help="Read-only verification of the exact prepared Development clone, before any application writer starts")
parser.add_argument("--verify-final-development-restore", action="store_true", help="Verify the final Development archive against its dedicated isolated restore")
options = parser.parse_args()
RESTORED = "ifm_application_development" if options.verify_development_clone else "ifm_storage_migration_staging_restored_20261012"
PROOF_NAME = "development-clone-row-proof-20261012.json" if options.verify_development_clone else "consolidated-isolated-restore-row-proof-20261012.json"
if options.verify_final_development_restore:
    if options.verify_development_clone:
        parser.error("Select only one verification mode")
    SOURCE = "ifm_application_development"
    RESTORED = "ifm_storage_final_restore_20261012"
    PROOF_NAME = "final-development-restore-row-proof-20261012.json"
ARTIFACTS = Path(__file__).resolve().parents[3] / ".artifacts" / "storage-cutover"

def query(database, sql):
    if database not in (SOURCE, RESTORED):
        raise ValueError("Only the exact isolated staging databases are accepted")
    result = subprocess.run(["docker", "exec", "-i", CONTAINER, "psql", "-X", "-v", "ON_ERROR_STOP=1", "-U", "postgres", "-d", database, "-At"], input=sql, text=True, capture_output=True, check=True)
    return result.stdout.strip()

def identifier(name):
    if not re.fullmatch(r"[a-zA-Z_][a-zA-Z0-9_]*", name):
        raise ValueError("Unexpected identifier in owned schema metadata")
    return '"' + name + '"'

inventory = "SELECT json_agg(json_build_object('schema',table_schema,'name',table_name) ORDER BY table_schema,table_name) FROM information_schema.tables WHERE table_type='BASE TABLE' AND table_schema NOT IN ('pg_catalog','information_schema');"
tables = json.loads(query(SOURCE, inventory))
if tables != json.loads(query(RESTORED, inventory)):
    raise RuntimeError("Restored table inventory differs")
proof = []
for table in tables:
    name = identifier(table['schema']) + '.' + identifier(table['name'])
    sql = "SELECT json_build_object('rows',count(*),'orderedRowDigest',md5(COALESCE(string_agg(row_hash,'' ORDER BY row_hash),''))) FROM (SELECT md5(row_to_json(t)::text) AS row_hash FROM " + name + " t) rows;"
    before = json.loads(query(SOURCE, sql))
    actual = json.loads(query(RESTORED, sql))
    after = json.loads(query(SOURCE, sql))
    if before != after or before != actual:
        raise RuntimeError("Complete restored row mismatch: " + name)
    proof.append(dict(table=table['schema']+'.'+table['name'], **actual))
    if len(proof) % 20 == 0:
        print(json.dumps(dict(tablesVerified=len(proof), rowsVerified=sum(t['rows'] for t in proof))), flush=True)
    (ARTIFACTS / PROOF_NAME).write_text(json.dumps(dict(sourceDatabase=SOURCE,restoredDatabase=RESTORED,tablesVerified=len(proof),rowsVerified=sum(t['rows'] for t in proof),sourceUnchanged=True,tables=proof),indent=2),encoding='utf-8')
print(json.dumps(dict(restoreVerification="passed",tables=len(proof),rows=sum(t['rows'] for t in proof))),flush=True)
