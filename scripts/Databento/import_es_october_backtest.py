"""Clone the raw-tick schema and import October 2026 ES or VX outright futures trades.

Host commands: schema, download, prepare. Container commands: load, verify.
The source keyspace is read only. Downloads require a fresh zero-dollar estimate.
Raw DBN archives, deterministic CSVs, checkpoints and verification proofs are retained.
"""
import argparse
import base64
import collections
import datetime as dt
import decimal
import hashlib
import functools
import json
import os
import re
from pathlib import Path
import subprocess
import time
import urllib.parse
import urllib.request
from zoneinfo import ZoneInfo

SOURCE = "trade_tick_quote_test_db"
TARGET = "trade_tick_quote_backtest_db"
CONTAINER = "ifm-scylladb"
START = "2026-10-01T00:00:00Z"
END = "2026-11-01T00:00:00Z"
SYMBOL = "ES"
PRESERVE_MANIFEST = None
LOAD_WORKERS = 1
VERIFY_WORKERS = 4
DATASET = "GLBX.MDP3"
TABLES = ("futures_tick_data", "futures_tick_data_by_time")
COLUMNS = "contractid,valuedate,tickid,ticktime,price,size"


def save(path, value):
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, indent=2, sort_keys=True), encoding="utf-8")
    temporary.replace(path)


def sha(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def cql(query):
    return subprocess.run(["docker", "exec", "-i", CONTAINER, "cqlsh"],
                          input=query, text=True, capture_output=True, check=True).stdout


def schema(root):
    original = cql("DESCRIBE KEYSPACE " + SOURCE + ";\n")
    if "CREATE KEYSPACE " + SOURCE not in original or "CREATE TABLE " not in original:
        raise RuntimeError("Source schema was not returned")
    if "(0 rows)" not in cql("SELECT keyspace_name FROM system_schema.keyspaces WHERE keyspace_name='" + TARGET + "';\n"):
        raise RuntimeError("Target already exists; refuse to replace its schema")
    (root / "source-schema.cql").write_text(original, encoding="utf-8")
    target = original.replace(SOURCE, TARGET)
    (root / "backtest-schema.cql").write_text(target, encoding="utf-8")
    cql(target)
    actual = cql("DESCRIBE KEYSPACE " + TARGET + ";\n")
    # DESCRIBE output may reorder objects, so compare individual CQL definitions.
    canonical = lambda s: sorted(" ".join(x.split()) for x in s.split(";") if x.strip())
    if canonical(actual.replace(TARGET, SOURCE)) != canonical(original):
        raise RuntimeError("Cloned schema differs from the source")
    save(root / "schema-proof.json", {"source": SOURCE, "target": TARGET,
         "same_definitions": True, "tables": original.count("CREATE TABLE "),
         "types": original.count("CREATE TYPE "), "source_schema_sha256": sha(root / "source-schema.cql")})
    print("Cloned and verified all source table/type definitions", flush=True)


def request(method, fields, post=False):
    key = os.environ["DATABENTO_API_KEY"]
    headers = {"Authorization": "Basic " + base64.b64encode((key + ":").encode()).decode()}
    encoded = urllib.parse.urlencode(fields).encode()
    url = "https://hist.databento.com/v0/" + method
    if post:
        headers["Content-Type"] = "application/x-www-form-urlencoded"
    else:
        url += "?" + encoded.decode()
    return urllib.request.urlopen(urllib.request.Request(url, data=encoded if post else None,
                                  headers=headers), timeout=180)


def download(root):
    proof = {}
    for name in ("definition", "trades"):
        fields = dict(dataset=DATASET, symbols=SYMBOL + ".FUT", stype_in="parent",
                      start=START, end=END, schema=name)
        with request("metadata.get_cost", fields) as response:
            cost = decimal.Decimal(response.read().decode())
        if cost != 0:
            raise RuntimeError(f"Refusing a nonzero acquisition estimate for {name}: {cost}")
        with request("metadata.get_record_count", fields) as response:
            count = json.load(response)
        path = root / (name + ".dbn.zst")
        receipt = root / (name + "-download.json")
        if path.exists():
            old = json.loads(receipt.read_text())
            if any(old.get(k) != fields[k] for k in ("dataset", "start", "end", "schema")) or old.get("symbols") != SYMBOL + ".FUT":
                raise RuntimeError("Archive receipt belongs to another request")
            if old["sha256"] != sha(path) or old["records_estimate"] != count:
                raise RuntimeError("Existing archive differs from its download receipt")
            proof[name] = old
            continue
        print(f"Downloading {name}; {count:,} estimated records; cost ${cost}", flush=True)
        fields.update(encoding="dbn", compression="zstd", stype_out="instrument_id")
        temporary = path.with_suffix(path.suffix + ".partial")
        start = time.monotonic()
        with request("timeseries.get_range", fields, post=True) as response, temporary.open("wb") as output:
            while block := response.read(1024 * 1024):
                output.write(block)
        temporary.replace(path)
        proof[name] = dict(schema=name, dataset=DATASET, symbols=SYMBOL + ".FUT", start=START, end=END,
                           records_estimate=count, cost_usd=str(cost), bytes=path.stat().st_size,
                           sha256=sha(path), elapsed_seconds=round(time.monotonic()-start, 2))
        save(receipt, proof[name])
        print(json.dumps(proof[name]), flush=True)
    save(root / "acquisition.json", proof)


def prepare(root):
    import databento as db
    mappings = {}
    for record in db.DBNStore.from_file(root / "definition.dbn.zst"):
        if not hasattr(record, "raw_symbol"):
            continue
        instrument_class = str(record.instrument_class)
        if instrument_class not in ("F", "S"):
            continue
        expiry = dt.datetime.fromtimestamp(int(record.expiration)//1_000_000_000, dt.timezone.utc).date()
        raw = str(record.raw_symbol)
        canonical = SYMBOL + expiry.strftime("%Y%m%d") if instrument_class == "F" else raw
        instrument = int(record.instrument_id)
        old = mappings.get(instrument)
        item = {"contract_id": canonical, "raw_symbol": raw, "expiration": str(expiry), "instrument_class": instrument_class}
        if old is not None and old != item:
            raise RuntimeError(f"Ambiguous instrument definition {instrument}: {old}, {item}")
        mappings[instrument] = item
    if not mappings:
        raise RuntimeError(f"No {SYMBOL} outright future definitions")
    save(root / "contract-mappings.json", mappings)
    output = root / "csv"
    output.mkdir(exist_ok=True)
    if any(output.glob("*.csv")):
        raise RuntimeError("CSV files already exist; refuse to overwrite an import input")
    handles = collections.OrderedDict()
    counts = collections.Counter()
    summaries = {}
    last_id = {}
    market = ZoneInfo("America/New_York")
    @functools.lru_cache(maxsize=8192)
    def local_parts(seconds):
        local = dt.datetime.fromtimestamp(seconds, dt.timezone.utc).astimezone(market)
        value_date = local.date() + dt.timedelta(days=int(local.hour >= 18))
        lower = dt.datetime.fromisoformat(START.replace("Z", "+00:00")).date() - dt.timedelta(days=1)
        upper = dt.datetime.fromisoformat(END.replace("Z", "+00:00")).date() + dt.timedelta(days=1)
        if not lower <= value_date <= upper:
            raise RuntimeError(f"Unexpected trading date {value_date}")
        return str(value_date), local.strftime("%H:%M:%S")
    total = adjusted = excluded_spreads = 0
    class_counts = collections.Counter()
    for record in db.DBNStore.from_file(root / "trades.dbn.zst"):
        if not hasattr(record, "price") or not hasattr(record, "size"):
            continue
        if str(record.action) != "T":
            raise RuntimeError(f"Unexpected action in trades schema: {record.action}")
        instrument = int(record.instrument_id)
        if instrument not in mappings:
            raise RuntimeError(f"Unmapped {SYMBOL} instrument {instrument}")
        if mappings[instrument]["instrument_class"] != "F":
            excluded_spreads += 1
            continue
        contract = mappings[instrument]["contract_id"]
        ns = int(record.ts_event)
        seconds, nanos = divmod(ns, 1_000_000_000)
        value_date, local_clock = local_parts(seconds)
        tick_id = max(ns, last_id.get(contract, 0) + 1)
        adjusted += int(tick_id != ns)
        last_id[contract] = tick_id
        tick_time = local_clock + f".{nanos:09d}"
        raw_price, size = int(record.price), int(record.size)
        if abs(raw_price) >= 2**63-1 or not 0 < size <= 2**31-1 or (mappings[instrument]["instrument_class"] == "F" and raw_price <= 0):
            raise RuntimeError("Invalid trade price or size")
        price = ("-" if raw_price < 0 else "") + f"{abs(raw_price) // 1_000_000_000}.{abs(raw_price) % 1_000_000_000:09d}"
        partition = (contract, str(value_date))
        name = f"{contract}_{value_date}.csv"
        if name not in handles:
            if len(handles) >= 128:
                _, old_handle = handles.popitem(last=False)
                old_handle.close()
            handles[name] = (output / name).open("a", encoding="ascii", newline="")
        handles.move_to_end(name)
        line = f"{contract},{value_date},{tick_id},{tick_time},{price},{size}\n"
        handles[name].write(line)
        counts[partition] += 1
        summary = summaries.setdefault(partition, dict(contract_id=contract, value_date=str(value_date),
            file="csv/"+name, rows=0, min_tick_id=tick_id, max_tick_id=tick_id,
            min_event_ns=ns, max_event_ns=ns, volume=0, price_raw_sum=0, row_hash_sum=0))
        summary["rows"] += 1
        summary["max_tick_id"] = tick_id
        summary["min_event_ns"] = min(summary["min_event_ns"], ns)
        summary["max_event_ns"] = max(summary["max_event_ns"], ns)
        summary["volume"] += size
        summary["price_raw_sum"] += raw_price
        summary["row_hash_sum"] = (summary["row_hash_sum"] + int.from_bytes(hashlib.sha256(line.encode("ascii")).digest(), "big")) % (1 << 256)
        total += 1
        class_counts[mappings[instrument]["instrument_class"]] += 1
        if total % 500_000 == 0:
            print(f"Normalized {total:,} trade records", flush=True)
    for handle in handles.values():
        handle.close()
    estimated = json.loads((root / "trades-download.json").read_text())["records_estimate"]
    if total + excluded_spreads != estimated:
        raise RuntimeError(f"Downloaded trade count {total + excluded_spreads} differs from metadata {estimated}")
    for summary in summaries.values():
        summary["sha256"] = sha(root / summary["file"])
    manifest = dict(source=SOURCE, target=TARGET, dataset=DATASET, parent=SYMBOL + ".FUT", start=START, end=END,
        total_rows=total, partitions=list(summaries.values()), tables=list(TABLES),
        value_date="America/New_York, next date at 18:00, matching CmeFuturesMarketSessionCalendar",
        tick_time="America/New_York market time; original event nanoseconds retained",
        tick_id="max(event_timestamp_ns, previous_contract_tick_id + 1), stable source-file ordering",
        instrument_class_counts=dict(class_counts), excluded_spread_records=excluded_spreads,
        included_instruments=SYMBOL + " outright futures only, definition instrument_class F; no options or spreads",
        adjusted_tick_ids=adjusted, raw_trade_archive_sha256=sha(root / "trades.dbn.zst"))
    if PRESERVE_MANIFEST is not None:
        preserved = json.loads(Path(PRESERVE_MANIFEST).read_text())
        if (preserved['target'], preserved['dataset'], preserved['parent']) != (TARGET, DATASET, SYMBOL+'.FUT'):
            raise RuntimeError('Preserved manifest has a different data scope')
        if END != preserved['start']:
            raise RuntimeError('Preserved import must begin at the new range exclusive end')
        manifest['preserved_partitions'] = preserved['partitions']
        manifest['preserved_manifest_sha256'] = sha(Path(PRESERVE_MANIFEST))
        # Validate every overlapping partition before any database write.
        verification_partitions(manifest)
    save(root / "import-manifest.json", manifest)
    print(f"Prepared {total:,} records in {len(summaries)} contract/date partitions", flush=True)


def verification_partitions(manifest):
    """Merge disjoint UTC slices sharing a NY value-date partition."""
    expected = {}
    for p in manifest.get('preserved_partitions', []):
        key = (p['contract_id'],p['value_date'])
        expected[key] = dict(p, load_required=False)
    for p in manifest['partitions']:
        key = (p['contract_id'],p['value_date'])
        old = expected.get(key)
        merged = dict(p, load_required=True)
        if old:
            if not (p['max_tick_id'] < old['min_tick_id'] or old['max_tick_id'] < p['min_tick_id']):
                raise RuntimeError('Overlapping tick-key ranges; refuse to overwrite preserved rows: '+str(key))
            for field in ('rows','volume','price_raw_sum'):
                merged[field] += old[field]
            merged['row_hash_sum'] = (p['row_hash_sum'] + old['row_hash_sum']) % (1 << 256)
            for field in ('min_tick_id','min_event_ns'):
                merged[field] = min(p[field],old[field])
            for field in ('max_tick_id','max_event_ns'):
                merged[field] = max(p[field],old[field])
        expected[key] = merged
    return list(expected.values())


def connect():
    from cassandra.cluster import Cluster
    import socket
    cluster = Cluster([socket.gethostbyname(socket.gethostname())])
    session = cluster.connect(TARGET)
    session.default_timeout = 600
    session.default_fetch_size = 10_000
    return cluster, session


def load(root):
    import concurrent.futures
    import threading
    manifest = json.loads((root / "import-manifest.json").read_text())
    if manifest["target"] != TARGET:
        raise RuntimeError("Wrong manifest target")
    checkpoint = root / "load-checkpoint.json"
    done = json.loads(checkpoint.read_text()) if checkpoint.exists() else {}
    expected_partitions = {(p['contract_id'],p['value_date']):p for p in verification_partitions(manifest)}
    cluster, session = connect()
    checkpoint_lock = threading.Lock()
    try:
        for partition in manifest["partitions"]:
            path = root / partition["file"]
            if sha(path) != partition["sha256"]:
                raise RuntimeError("CSV checksum mismatch")
        dates = sorted(set(p["value_date"] for p in manifest["partitions"]))
        def load_date(job):
                table, date = job
                partitions = [p for p in manifest["partitions"] if p["value_date"] == date]
                name = table + "/" + date
                input_hash = hashlib.sha256("".join(p["sha256"] for p in partitions).encode()).hexdigest()
                # This cqlsh version silently ignores wildcard COPY commands.
                # Explicit files also restrict acquisition to the verified manifest.
                paths = ",".join(str(root / p["file"]) for p in partitions)
                if name in done:
                    if done[name]["sha256"] != input_hash:
                        raise RuntimeError("Checkpoint belongs to another input")
                    return
                # Exact deterministic keys make a retry after interruption idempotent.
                query = (f"COPY {TARGET}.{table} ({COLUMNS}) FROM '{paths}' WITH HEADER=false "
                         "AND NUMPROCESSES=4 AND CHUNKSIZE=1000 AND INGESTRATE=35000 "
                         "AND MAXBATCHSIZE=100 AND MAXINFLIGHTMESSAGES=4096 "
                         "AND REQUESTTIMEOUT=240 AND CHILDTIMEOUT=300 "
                         "AND MAXATTEMPTS=10 AND MAXPARSEERRORS=0 AND MAXINSERTERRORS=0 "
                         "AND REPORTFREQUENCY=1 AND ERRFILE='" + str(root / "copy-errors.csv") + "';")
                started = time.monotonic()
                log = root / (table + "-" + date + ".log")
                expected = sum(p["rows"] for p in partitions)
                for copy_attempt in range(3):
                    copy_rate = 35000 // (2 ** copy_attempt)
                    attempt_query = query.replace('INGESTRATE=35000',f'INGESTRATE={copy_rate}')
                    result = subprocess.run(["cqlsh", "--request-timeout=600", "-e", attempt_query],
                                            text=True, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
                    log.write_text(result.stdout)
                    reports = re.findall(r"(?m)^(\d+) rows imported from [^\r\n]*\((\d+) skipped\)", result.stdout)
                    if reports and tuple(map(int,reports[-1])) == (expected,0):
                        break
                    (root / (table+'-'+date+f'-incomplete-{copy_attempt+1}.log')).write_text(result.stdout)
                    if copy_attempt < 2:
                        print(json.dumps(dict(retrying=name,next_ingest_rate=copy_rate//2,expected_rows=expected)),flush=True)
                        time.sleep(5*(copy_attempt+1))
                # Retried write timeouts may appear before a successful summary.
                # Require the exact final import count, zero skips and persisted counts.
                if not reports or tuple(map(int, reports[-1])) != (expected, 0):
                    raise RuntimeError("COPY failed; inspect " + str(log) + "\n" + result.stdout[-2000:])
                total = 0
                for partition in partitions:
                    contract = partition["contract_id"]
                    count = session.execute(f"SELECT COUNT(*) FROM {table} WHERE contractid=%s AND valuedate=%s",
                                            (contract, date)).one().count
                    expected_count = expected_partitions[(contract,date)]['rows']
                    if count != expected_count:
                        raise RuntimeError(f"{table}/{contract}/{date}: {count} persisted, expected {expected_count}")
                    total += count
                # cqlsh can retain exit status 1 after retrying a timeout even
                # when its final import summary is complete. Accept that only
                # after independently fingerprinting every persisted row.
                if result.returncode:
                    for partition in partitions:
                        verify_partition(table, expected_partitions[(partition['contract_id'],date)], session)
                with checkpoint_lock:
                    done[name] = dict(rows=total, sha256=input_hash, seconds=round(time.monotonic()-started, 2),
                                      copy_exit_status=result.returncode)
                    save(checkpoint, done)
                    print(json.dumps({"loaded": name, **done[name]}), flush=True)
        # The legacy first table partitions by contract alone: adjacent dates
        # therefore hit the same partition. Advance dates only after both tables
        # finish, while allowing their independent writes to run together.
        with concurrent.futures.ThreadPoolExecutor(max_workers=LOAD_WORKERS) as pool:
            for date in dates:
                list(pool.map(load_date, ((table,date) for table in TABLES)))
    finally:
        cluster.shutdown()


def verify(root):
    import concurrent.futures
    import multiprocessing
    manifest = json.loads((root / "import-manifest.json").read_text())
    partitions = verification_partitions(manifest)
    groups = collections.defaultdict(list)
    for partition in partitions:
        groups[partition['contract_id']].append(partition)
    contract_groups = sorted(groups.values(), key=lambda ps: sum(p['rows'] for p in ps), reverse=True)
    expected_rows = sum(p['rows'] for p in partitions)
    result = {"target": TARGET, "new_rows_per_table": manifest["total_rows"], "expected_rows_per_table": expected_rows, "tables": {}}
    verified = {table: [] for table in TABLES}
    # The parent has no Cassandra connections or driver threads. Linux fork also
    # supports Scylla's bundled Python, whose standalone executable cannot spawn.
    context = "fork" if "fork" in multiprocessing.get_all_start_methods() else "spawn"
    with concurrent.futures.ProcessPoolExecutor(max_workers=VERIFY_WORKERS,
            mp_context=multiprocessing.get_context(context), initializer=initialize_verifier,
            initargs=(str(root),)) as pool:
        jobs = [pool.submit(verify_contract, table, ps) for table in TABLES for ps in contract_groups]
        try:
            for future in concurrent.futures.as_completed(jobs):
                table, rows = future.result()
                verified[table].extend(rows)
        except Exception:
            for pending in jobs:
                pending.cancel()
            raise
    for table, rows in verified.items():
        rows.sort(key=lambda row: (row["value_date"], row["contract_id"]))
        result["tables"][table] = dict(total_rows=sum(x["rows"] for x in rows), partitions=rows)
        if result["tables"][table]["total_rows"] != expected_rows:
            raise RuntimeError("Verified total differs from the import manifest")
    result["verified"] = True
    save(root / "verification.json", result)


def verify_contract(table, partitions):
    """Keep legacy contract-partition reads sequential within each worker."""
    rows = []
    for partition in sorted(partitions, key=lambda p: p['value_date']):
        try:
            _, row = verify_partition(table, partition)
        except Exception as error:
            print(f"Verification failed {table} {partition['contract_id']}/{partition['value_date']}: {error!r}",flush=True)
            raise
        rows.append(row)
        print(f"Verified {table} {row['contract_id']}/{row['value_date']}: {row['rows']:,} rows", flush=True)
    return table, rows


def initialize_verifier(root=None):
    import atexit
    global verification_cluster, verification_session, verification_root
    verification_root = Path(root) if root else None
    verification_cluster, verification_session = connect()
    atexit.register(verification_cluster.shutdown)


def verify_partition(table, partition, session=None):
    if session is None and verification_root is not None and partition.get('load_required', True):
        # Verification can overlap acquisition, but never reads a date before
        # COPY and the persisted-count check have completed for that date.
        checkpoint = verification_root / "load-checkpoint.json"
        deadline = time.monotonic() + 1800
        key = table + "/" + partition["value_date"]
        while not checkpoint.exists() or key not in json.loads(checkpoint.read_text()):
            if time.monotonic() >= deadline:
                raise TimeoutError("Waiting for completed import " + key)
            time.sleep(1)
    count = volume = price_sum = row_hash = 0
    minimum, maximum = 2**63-1, 0
    reader = session if session is not None else verification_session
    for row in reader.execute(
            f"SELECT tickid,ticktime,price,size FROM {table} WHERE contractid=%s AND valuedate=%s",
            (partition["contract_id"], partition["value_date"])):
        count += 1
        volume += row.size
        price_sum += int(row.price * decimal.Decimal(1_000_000_000))
        minimum, maximum = min(minimum, row.tickid), max(maximum, row.tickid)
        line = f"{partition['contract_id']},{partition['value_date']},{row.tickid},{row.ticktime},{row.price:.9f},{row.size}\n"
        row_hash = (row_hash + int.from_bytes(hashlib.sha256(line.encode("ascii")).digest(), "big")) % (1 << 256)
    actual = dict(rows=count, volume=volume, price_raw_sum=price_sum,
                  min_tick_id=minimum, max_tick_id=maximum, row_hash_sum=row_hash)
    for key, value in actual.items():
        if partition[key] != value:
            raise RuntimeError(f"Verification failed {table}/{partition['file']} {key}: {value} != {partition[key]}")
    return table, dict(contract_id=partition["contract_id"], value_date=partition["value_date"], **actual)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=["schema", "download", "prepare", "load", "verify"])
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--symbol", choices=["ES", "VX"], default="ES")
    parser.add_argument("--start", default=START)
    parser.add_argument("--end", default=END)
    parser.add_argument("--preserve-manifest", type=Path)
    parser.add_argument("--load-workers", type=int, choices=range(1,5), default=1)
    parser.add_argument("--verify-workers", type=int, choices=range(1,17), default=VERIFY_WORKERS)
    args = parser.parse_args()
    SYMBOL = args.symbol
    DATASET = "GLBX.MDP3" if SYMBOL == "ES" else "XCBF.PITCH"
    START, END = args.start, args.end
    if dt.datetime.fromisoformat(START.replace('Z','+00:00')) >= dt.datetime.fromisoformat(END.replace('Z','+00:00')):
        parser.error('start must precede end')
    PRESERVE_MANIFEST = args.preserve_manifest
    LOAD_WORKERS = args.load_workers
    VERIFY_WORKERS = args.verify_workers
    args.root.mkdir(parents=True, exist_ok=True)
    globals()[args.command](args.root.resolve())
