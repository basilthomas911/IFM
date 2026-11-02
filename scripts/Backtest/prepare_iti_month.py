"""Export October ES/VX as-of inputs from Scylla, one streaming CSV per trading day.

Cold start; no fabricated VX seed. The production rollover policy refreshes at
valueDate >= LastTradeDate, so VX October is replaced on value date October 21.
Only on-the-run ES December enters ITI, matching production ingress filtering.
"""
import argparse
import csv
import datetime as dt
import hashlib
import json
import socket
from pathlib import Path
from cassandra.cluster import Cluster
from prepare_iti_pilot import rows, KEYSPACE, ES


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--es-manifest', type=Path, required=True)
    args = parser.parse_args()
    root = args.output
    root.mkdir(parents=True, exist_ok=True)
    source = json.loads(args.es_manifest.read_text())
    partitions = sorted((p for p in source['partitions'] if p['contract_id'] == ES), key=lambda p: p['value_date'])
    if (root / 'input.json').exists() or any(root.glob('*.csv')):
        raise ValueError('Choose an empty output directory; replay inputs are immutable')
    contracts = [('2026-10-21', 'VX20261021'), ('2026-11-18', 'VX20261118')]
    carry = {}
    files = []
    total = skipped = 0
    cluster = Cluster([socket.gethostbyname(socket.gethostname())])
    try:
        session = cluster.connect(KEYSPACE)
        session.default_timeout = 180
        for partition in partitions:
            date = partition['value_date']
            es_rows = rows(session, ES, date)
            if len(es_rows) != partition['rows']:
                raise ValueError('ES source count differs from verified import manifest')
            vx_contract = next(contract for expiry, contract in contracts if expiry > date)
            vx_rows = {contract: rows(session, contract, date) for _, contract in contracts}
            current = vx_rows[vx_contract]
            latest, index = carry.get(vx_contract), 0
            count = missing = 0
            max_age = 0
            path = root / (date + '.csv')
            with path.open('w', newline='', encoding='ascii') as stream:
                writer = csv.writer(stream, lineterminator='\n')
                for ns, tickid, price, size in es_rows:
                    while index < len(current) and current[index][0] < ns:
                        latest = current[index]
                        index += 1
                    if latest is None:
                        missing += 1
                        continue
                    if latest[0] >= ns or price <= 0 or latest[2] <= 0:
                        raise ValueError('Invalid ES/VX as-of pair')
                    timestamp = dt.datetime.fromtimestamp(ns // 1_000_000_000, dt.timezone.utc)
                    timestamp = timestamp.strftime('%Y-%m-%dT%H:%M:%S') + f'.{ns % 1_000_000_000 // 100:07d}Z'
                    writer.writerow((date, timestamp, ns, tickid, price, latest[0], latest[1], latest[2], vx_contract))
                    count += 1
                    max_age = max(max_age, ns - latest[0])
            for contract, observations in vx_rows.items():
                if observations:
                    carry[contract] = observations[-1]
            digest = hashlib.sha256()
            with path.open('rb') as stream:
                for block in iter(lambda: stream.read(1024 * 1024), b''):
                    digest.update(block)
            item = dict(file=path.name, value_date=date, rows=count, source_rows=len(es_rows),
                skipped_no_prior_vx=missing, vx_contract_id=vx_contract,
                max_vx_age_seconds=max_age / 1e9, sha256=digest.hexdigest())
            files.append(item)
            total += count
            skipped += missing
            print(json.dumps(item), flush=True)
    finally:
        cluster.shutdown()
    proof = dict(format='iti-csv-v1', keyspace=KEYSPACE, contract_id=ES, files=files,
        start=source['start'], end=source['end'], es_rows=total,
        considered_es_rows=total+skipped, skipped_no_prior_vx=skipped,
        excluded_deferred_es_rows=source['total_rows']-total-skipped,
        initialization='cold start per Daily/Weekly/Monthly stream; no September warmup',
        vx_policy='latest strictly earlier trade; front maturity strictly after trading value date; carry prior prices across sessions',
        timestamp_policy='original nanoseconds retained; production command timestamp has 100ns precision')
    proof['input_sha256'] = hashlib.sha256(json.dumps(files, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    (root / 'input.json').write_text(json.dumps(proof, indent=2))
    print(json.dumps({k:v for k,v in proof.items() if k != 'files'}), flush=True)


if __name__ == '__main__':
    main()
