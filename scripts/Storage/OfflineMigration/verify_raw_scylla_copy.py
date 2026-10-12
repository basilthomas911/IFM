"""Offline local Scylla raw-table copy. Preserve source and refuse nonempty targets.
Run inside the explicitly named Development Scylla container after the reviewed
TradeTickQuote schema and tagged source snapshot have been created.
"""
import datetime
import decimal
import hashlib
import json
from pathlib import Path
import shutil
import socket
import subprocess
import uuid
from cassandra.cluster import Cluster
from cassandra import ConsistencyLevel
from cassandra.query import SimpleStatement
from cassandra.concurrent import execute_concurrent_with_args

SOURCE = "market_data_test_db"
TARGET = "trade_tick_quote_test_db"
TAG = "ifm-raw-cutover-20261012"
TABLES = ("tick_trade_data", "tick_quote_data", "futures_tick_data",
          "futures_tick_data_by_time", "futures_option_tick_data")
ROOT = Path("/var/lib/scylla/data")

def canonical(value):
    if value is None or isinstance(value, (str, bool, int)):
        return value
    if isinstance(value, float):
        return {"float": value.hex()}
    if isinstance(value, decimal.Decimal):
        return {"decimal": str(value)}
    if isinstance(value, (datetime.datetime, datetime.date)):
        return {"date": value.isoformat()}
    if isinstance(value, uuid.UUID):
        return {"uuid": str(value)}
    if isinstance(value, bytes):
        return {"bytes": value.hex()}
    if isinstance(value, dict):
        return sorted((canonical(k), canonical(v)) for k, v in value.items())
    if isinstance(value, (tuple, list)):
        return [canonical(v) for v in value]
    if isinstance(value, (set, frozenset)):
        return sorted(canonical(v) for v in value)
    # Cassandra LocalDate / LocalTime preserve their exact textual values.
    if type(value).__module__.startswith("cassandra"):
        return {type(value).__name__: str(value)}
    raise TypeError("Unsupported exact fingerprint type: " + str(type(value)))

def fingerprint(session, keyspace, table):
    statement = SimpleStatement("SELECT * FROM " + keyspace + "." + table,
                                consistency_level=ConsistencyLevel.LOCAL_QUORUM,
                                fetch_size=512)
    total = count = 0
    for row in session.execute(statement, timeout=600):
        body = json.dumps(canonical(row), sort_keys=True, separators=(",", ":"),
                          ensure_ascii=True).encode()
        total = (total + int.from_bytes(hashlib.sha256(body).digest(), "big")) % (1 << 256)
        count += 1
    return {"rows": count, "rowHashSum": format(total, "064x")}

def columns(session, keyspace, table):
    rows = session.execute("SELECT column_name, kind, position, type FROM system_schema.columns "
                           "WHERE keyspace_name=%s AND table_name=%s", (keyspace, table))
    return sorted((r.column_name, r.kind, r.position,
                   r.type.replace(keyspace + ".", "")) for r in rows)

with Cluster([socket.gethostbyname(socket.gethostname())]) as cluster:
    session = cluster.connect()
    prior_path = Path("/tmp/ifm-raw-copy-proof-20261012.json")
    prior = json.loads(prior_path.read_text())["tables"] if prior_path.exists() else []
    proof = []
    for table in TABLES:
        if columns(session, SOURCE, table) != columns(session, TARGET, table):
            raise RuntimeError("Raw schema mismatch: " + table)
        existing = fingerprint(session, TARGET, table)
        accepted = next((item for item in prior if item["table"] == table), None)
        if accepted:
            if existing != accepted["target"] or fingerprint(session, SOURCE, table) != accepted["source"]:
                raise RuntimeError("Previously verified raw table changed: " + table)
            proof.append(accepted)
            print(json.dumps({"verifiedResume": table, "rows": existing["rows"]}), flush=True)
            continue
        if table == "tick_quote_data":
            # UDT serialization headers embed the keyspace name; native cross-keyspace loading is rejected.
            # This task owns the new target. A partial logical import can resume by writing identical source keys.
            source_query = SimpleStatement("SELECT * FROM " + SOURCE + "." + table,
                                           consistency_level=ConsistencyLevel.LOCAL_QUORUM, fetch_size=512)
            pending = []
            insert = None
            total = count = 0
            for row in session.execute(source_query, timeout=600):
                if insert is None:
                    names = list(row._fields)
                    insert = session.prepare("INSERT INTO " + TARGET + "." + table + " (" + ",".join(names) +
                                             ") VALUES (" + ",".join("?" for _ in names) + ")")
                    insert.consistency_level = ConsistencyLevel.LOCAL_QUORUM
                body = json.dumps(canonical(row), sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode()
                total = (total + int.from_bytes(hashlib.sha256(body).digest(), "big")) % (1 << 256)
                count += 1
                pending.append(tuple(row))
                if len(pending) == 256:
                    execute_concurrent_with_args(session, insert, pending, concurrency=16, raise_on_first_error=True)
                    pending.clear()
                    if count % 65536 == 0:
                        print(json.dumps({"logicalCopy": table, "rowsCopied": count}), flush=True)
            if pending:
                execute_concurrent_with_args(session, insert, pending, concurrency=16, raise_on_first_error=True)
            before = {"rows": count, "rowHashSum": format(total, "064x")}
            actual = fingerprint(session, TARGET, table)
            after = fingerprint(session, SOURCE, table)
            if before != actual or before != after:
                raise RuntimeError("Logical raw UDT copy mismatch")
            proof.append({"table": table, "source": before, "target": actual,
                          "copyMethod": "bounded-cql-udt-import", "sourceUnchanged": True})
            prior_path.write_text(json.dumps({"sourceKeyspace": SOURCE, "targetKeyspace": TARGET,
                                              "snapshotTag": TAG, "tables": proof}, indent=2))
            print(json.dumps({"verified": table, "rows": count}), flush=True)
            continue
        if existing["rows"]:
            raise RuntimeError("Refusing to modify nonempty raw target: " + table)
        source = list((ROOT / SOURCE).glob(table + "-*/snapshots/" + TAG))
        target = list((ROOT / TARGET).glob(table + "-*"))
        if len(source) != 1 or len(target) != 1:
            raise RuntimeError("Ambiguous snapshot or target directory: " + table)
        before = fingerprint(session, SOURCE, table)
        upload = target[0] / "upload"
        upload.mkdir(exist_ok=True)
        copied = []
        for file in sorted(source[0].iterdir()):
            if not file.is_file() or file.name in ("schema.cql", "manifest.json"):
                continue
            destination = upload / file.name
            if destination.exists():
                raise RuntimeError("Refusing to overwrite upload file: " + str(destination))
            shutil.copy2(file, destination)
            digest = hashlib.file_digest(file.open("rb"), "sha256").hexdigest()
            if digest != hashlib.file_digest(destination.open("rb"), "sha256").hexdigest():
                raise RuntimeError("Snapshot copy checksum mismatch")
            copied.append({"name": file.name, "bytes": file.stat().st_size, "sha256": digest})
        refresh = subprocess.run(["nodetool", "refresh", TARGET, table],
                                 stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
        if refresh.returncode:
            raise RuntimeError("Native raw refresh failed: " + refresh.stderr[-4000:])
        actual = fingerprint(session, TARGET, table)
        after = fingerprint(session, SOURCE, table)
        if before != after or before != actual:
            raise RuntimeError("Raw source/target count or complete-row mismatch: " + table)
        item = {"table": table, "source": before, "target": actual,
                "snapshotFiles": copied, "sourceUnchanged": True}
        proof.append(item)
        print(json.dumps({"verified": table, "rows": actual["rows"]}), flush=True)
        Path("/tmp/ifm-raw-copy-proof-20261012.json").write_text(
            json.dumps({"sourceKeyspace": SOURCE, "targetKeyspace": TARGET,
                        "snapshotTag": TAG, "tables": proof}, indent=2))
