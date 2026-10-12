"""Restore original Trade rows after a legacy fixture touched Development tables.
Only the nine audited tables are accepted. Data is read from the previously
verified consolidated migration database export; existing unrelated rows remain.
"""
import datetime
import json
from pathlib import Path
import socket
import uuid
from cassandra.cluster import Cluster
from cassandra.query import SimpleStatement
from cassandra import ConsistencyLevel
from cassandra.util import Date

ROOT = Path("/tmp/ifm-legacy-fixture-recovery-20261012")
TABLES = ("trade_order", "order_execution", "order_execution_fill", "established_trade",
          "established_trade_history", "strategy_position_current", "strategy_position_history",
          "open_position_route", "open_position_route_recovery")

def decode(value, kind):
    if value is None: return None
    if kind == "uuid": return uuid.UUID(value)
    if kind == "blob":
        if not value.startswith("\\x"): raise ValueError("Unexpected bytea encoding")
        return bytes.fromhex(value[2:])
    if kind == "date": return Date(value)
    if kind == "timestamp": return datetime.datetime.fromisoformat(value.replace("Z", "+00:00"))
    if kind in ("text", "varchar", "ascii") and not isinstance(value, str):
        return json.dumps(value, separators=(",", ":"))
    return value

with Cluster([socket.gethostbyname(socket.gethostname())]) as cluster:
    session = cluster.connect("trade_test_db")
    proof = []
    for table in TABLES:
        metadata = list(session.execute("SELECT column_name,kind,position,type FROM system_schema.columns "
                                       "WHERE keyspace_name=%s AND table_name=%s", ("trade_test_db", table)))
        types = {r.column_name: r.type for r in metadata}
        keys = [r.column_name for r in sorted(metadata, key=lambda r: (r.kind, r.position))
                if r.kind in ("partition_key", "clustering")]
        rows = [json.loads(line) for line in (ROOT / (table + ".jsonl")).read_text().splitlines() if line]
        before_count = session.execute("SELECT count(*) FROM " + table, timeout=600).one()[0]
        for row in rows:
            if not set(types).issubset(row): raise RuntimeError("Recovery source columns absent: " + table)
            extras = set(row) - set(types)
            allowed_defaults = {"projection_sequence": 0} if table == "strategy_position_history" else {}
            if any(row[name] is not None and row[name] != allowed_defaults.get(name) for name in extras):
                raise RuntimeError("Recovery would discard nonnull added target fields: " + table)
            names = sorted(types)
            query = session.prepare("INSERT INTO " + table + " (" + ",".join(names) + ") VALUES (" +
                                    ",".join("?" for _ in names) + ")")
            query.consistency_level = ConsistencyLevel.LOCAL_QUORUM
            session.execute(query, [decode(row[n], types[n]) for n in names])
            check = session.prepare("SELECT " + ",".join(names) + " FROM " + table + " WHERE " +
                                    " AND ".join(k + "=?" for k in keys))
            saved = session.execute(check, [decode(row[k], types[k]) for k in keys]).one()
            expected = [decode(row[n], types[n]) for n in names]
            for value, wanted, name in zip(saved, expected, names):
                if isinstance(wanted, datetime.datetime): wanted = wanted.replace(tzinfo=None)
                if value != wanted: raise RuntimeError("Restored value mismatch: " + table + "." + name)
        proof.append({"table": table, "originalRowsRestoredAndVerified": len(rows),
                      "rowsBeforeRepair": before_count, "unrelatedRowsDeleted": False})
        print(json.dumps(proof[-1]), flush=True)
    (ROOT / "restored-original-trade-rows-proof.json").write_text(json.dumps(proof, indent=2))
