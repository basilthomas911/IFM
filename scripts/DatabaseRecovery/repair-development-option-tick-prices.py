"""Run inside the Scylla container with application writers stopped.
Default mode only inspects. --apply preserves raw rows and a native snapshot,
then repairs the verified Development singleton. Never targets production.
"""
import argparse
import hashlib
import json
import math
import struct
import socket
import subprocess
from decimal import Decimal, localcontext

KEYSPACE = "market_data_test_db"
TABLE = "futures_option_tick_data"
ARCHIVE = "futures_option_tick_data_legacy_20261010"
SNAPSHOT = "ifm_option_tick_decimal_repair_20261010"
PRICES = ("optionprice", "bidprice", "askprice", "underlyingprice")
TYPES = {"contractid": "text", "valuedate": "date", "tickid": "bigint",
         "ticktime": "time", "optionprice": "decimal", "bidprice": "decimal",
         "askprice": "decimal", "underlyingprice": "decimal", "bidsize": "int",
         "asksize": "int", "impliedvolatility": "double", "delta": "double",
         "gamma": "double", "vega": "double", "theta": "double", "rho": "double"}
FIXTURE_KEY = ("ES20180511P2525", "2025-02-20", 123456)
FIXTURE_BYTES = {"optionprice": "4059200000000000", "bidprice": "4059000000000000",
                 "askprice": "4059400000000000", "underlyingprice": "4062c00000000000"}


def decode_price(raw, column, key):
    """Decode CQL decimal (signed scale + signed integer) to model double.
    Only the exact known fixture key AND payload permit IEEE double decoding.
    Unknown malformed decimals fail before any original table is changed.
    """
    if raw is None:
        return None
    raw = bytes(raw)
    if key == FIXTURE_KEY and raw.hex() == FIXTURE_BYTES[column]:
        return struct.unpack(">d", raw)[0]
    if len(raw) < 5:
        raise ValueError("Malformed decimal payload")
    scale = int.from_bytes(raw[:4], "big", signed=True)
    if not -28 <= scale <= 28:
        raise ValueError("Unknown decimal encoding; preserving original table")
    integer = int.from_bytes(raw[4:], "big", signed=True)
    with localcontext() as context:
        context.prec = max(50, len(str(abs(integer))) + 30)
        value = float(Decimal(integer).scaleb(-scale))
    if not math.isfinite(value):
        raise ValueError("Price cannot be represented as a finite double")
    return value


def ddl(table, price_type):
    columns = ", ".join(name + " " + (price_type if name in PRICES else kind)
                        for name, kind in TYPES.items())
    return ("CREATE TABLE " + KEYSPACE + "." + table + " (" + columns
            + ", PRIMARY KEY (contractid, valuedate, tickid))"
            + " WITH CLUSTERING ORDER BY (valuedate ASC, tickid ASC)")


def fingerprint(rows):
    def scalar(value):
        return bytes(value).hex() if isinstance(value, (bytes, bytearray)) else str(value)
    normalized = [{k: scalar(v) if v is not None else None for k, v in sorted(r.items())}
                  for r in rows]
    return hashlib.sha256(json.dumps(normalized, sort_keys=True).encode()).hexdigest()


def main(apply):
    from cassandra.cluster import Cluster
    cluster = Cluster([socket.gethostbyname(socket.gethostname())])
    try:
        session = cluster.connect()
        actual = {r.column_name: r.type for r in session.execute(
            "SELECT column_name,type FROM system_schema.columns WHERE keyspace_name=%s AND table_name=%s",
            (KEYSPACE, TABLE))}
        corrected = dict(TYPES, **{p: "double" for p in PRICES})
        if actual == corrected:
            list(session.execute("SELECT * FROM " + KEYSPACE + "." + TABLE + " LIMIT 1"))
            print(json.dumps({"status": "already_repaired", "keyspace": KEYSPACE, "table": TABLE}))
            return
        if actual != TYPES:
            raise ValueError("Unexpected source schema; no changes made")
        fields = ["decimalAsBlob(" + c + ") AS " + c if c in PRICES else c for c in TYPES]
        rows = [dict(r._asdict()) for r in session.execute(
            "SELECT " + ",".join(fields) + " FROM " + KEYSPACE + "." + TABLE + " LIMIT 2")]
        if len(rows) != 1:
            raise ValueError("Repair is scoped to the verified single Development fixture")
        row = rows[0]
        key = (row["contractid"], str(row["valuedate"]), row["tickid"])
        if key != FIXTURE_KEY:
            raise ValueError("Unexpected row; no changes made")
        converted = dict(row)
        for column in PRICES:
            converted[column] = decode_price(row[column], column, key)
        source_hash = fingerprint(rows)
        if not apply:
            print(json.dumps({"status": "inspected", "rows": 1, "source_sha256": source_hash,
                              "converted_prices": {c: converted[c] for c in PRICES}}))
            return
        if session.execute("SELECT table_name FROM system_schema.tables WHERE keyspace_name=%s AND table_name=%s",
                           (KEYSPACE, ARCHIVE)).one():
            raise ValueError("Archive already exists; inspect previous repair before continuing")
        schema = cluster.metadata.keyspaces[KEYSPACE].tables[TABLE].export_as_string()
        print(json.dumps({"original_schema": schema, "source_sha256": source_hash}), flush=True)
        subprocess.run(["nodetool", "snapshot", "-t", SNAPSHOT, "-cf", TABLE, KEYSPACE], check=True)
        session.execute(ddl(ARCHIVE, "blob"))
        names = list(TYPES)
        insert = lambda table: session.prepare("INSERT INTO " + KEYSPACE + "." + table + " ("
                      + ",".join(names) + ") VALUES (" + ",".join("?" for _ in names) + ")")
        session.execute(insert(ARCHIVE), [row[n] for n in names])
        archive = [dict(r._asdict()) for r in session.execute("SELECT * FROM " + KEYSPACE + "." + ARCHIVE)]
        if fingerprint(archive) != source_hash:
            raise ValueError("Archive verification failed; original preserved")
        current = [dict(r._asdict()) for r in session.execute(
            "SELECT " + ",".join(fields) + " FROM " + KEYSPACE + "." + TABLE + " LIMIT 2")]
        if fingerprint(current) != source_hash:
            raise ValueError("Source changed; stop writers before repairing")
        session.execute("DROP TABLE " + KEYSPACE + "." + TABLE)
        session.execute(ddl(TABLE, "double"))
        session.execute(insert(TABLE), [converted[n] for n in names])
        repaired = [dict(r._asdict()) for r in session.execute("SELECT * FROM " + KEYSPACE + "." + TABLE)]
        if fingerprint(repaired) != fingerprint([converted]):
            raise ValueError("Repaired table verification failed; archive and native snapshot preserved")
        print(json.dumps({"status": "repaired", "rows": len(repaired), "archive": KEYSPACE + "." + ARCHIVE,
                          "snapshot_tag": SNAPSHOT, "original_sha256": source_hash,
                          "repaired_sha256": fingerprint(repaired),
                          "prices": {c: repaired[0][c] for c in PRICES}}))
    finally:
        cluster.shutdown()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    main(args.apply)
