"""Read-only Scylla adapter for a bounded ITI integration replay (run inside Scylla).

The first pilot explicitly selects the October 1 front contracts, rather than
using today's rollover assignments. VX at the same event nanosecond is deferred
until the next ES event because cross-venue ordering at equal times is unknown.
"""
import argparse
import datetime as dt
import hashlib
import json
import socket
from pathlib import Path
from zoneinfo import ZoneInfo
from cassandra.cluster import Cluster

KEYSPACE = 'trade_tick_quote_backtest_db'
DATE = '2026-10-01'
ES = 'ES20261218'
VX = 'VX20261021'
NY = ZoneInfo('America/New_York')


def rows(session, contract, value_date=DATE):
    result = []
    for row in session.execute(
            f'SELECT tickid,ticktime,price,size FROM {KEYSPACE}.futures_tick_data '
            'WHERE contractid=%s AND valuedate=%s', (contract, value_date)):
        nanos = row.ticktime.nanosecond_time
        date = dt.date.fromisoformat(value_date)
        if nanos >= 18 * 3600 * 1_000_000_000:
            date -= dt.timedelta(days=1)
        midnight = dt.datetime.combine(date, dt.time(), NY)
        ns = int(midnight.timestamp()) * 1_000_000_000 + nanos
        result.append((ns, row.tickid, row.price, row.size))
    return sorted(result)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--limit', type=int, default=5000)
    args = parser.parse_args()
    if not 1 <= args.limit <= 10000:
        raise ValueError('Pilot limit must be between 1 and 10,000 ES trades')
    cluster = Cluster([socket.gethostbyname(socket.gethostname())])
    try:
        session = cluster.connect(KEYSPACE)
        session.default_timeout = 120
        es, vx = rows(session, ES), rows(session, VX)
    finally:
        cluster.shutdown()
    start = int(dt.datetime(2026, 10, 1, 14, tzinfo=dt.timezone.utc).timestamp()) * 1_000_000_000
    end = start + 3600 * 1_000_000_000
    selected = [row for row in es if start <= row[0] < end][:args.limit]
    if len(selected) != args.limit or not vx:
        raise ValueError('Insufficient historical ES/VX ticks for the pilot')
    inputs, index, latest = [], 0, None
    for ns, tickid, price, size in selected:
        while index < len(vx) and vx[index][0] < ns:
            latest = vx[index]
            index += 1
        if latest is None:
            raise ValueError('No prior VX trade: refusing a fabricated volatility input')
        timestamp = dt.datetime.fromtimestamp(ns // 1_000_000_000, dt.timezone.utc)
        timestamp = timestamp.strftime('%Y-%m-%dT%H:%M:%S') + f'.{(ns % 1_000_000_000)//100:07d}Z'
        inputs.append(dict(tick_id=str(tickid), event_ns=str(ns), timestamp=timestamp,
            price=float(price), size=size, vx_tick_id=str(latest[1]), vx_event_ns=str(latest[0]),
            vx_price=float(latest[2])))
    proof = dict(keyspace=KEYSPACE, source_table='futures_tick_data', contract_id=ES,
        vx_contract_id=VX, value_date=DATE, initialization='cold-start pilot; no prior-day state',
        vx_policy='latest strictly earlier event timestamp; tick ID breaks within-contract ties',
        es_rows=len(inputs), max_vx_age_seconds=max((int(x['event_ns'])-int(x['vx_event_ns']))/1e9 for x in inputs),
        inputs=inputs)
    proof['input_sha256'] = hashlib.sha256(json.dumps(inputs, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    args.output.write_text(json.dumps(proof, indent=2))
    print(json.dumps({k: v for k, v in proof.items() if k != 'inputs'}), flush=True)


if __name__ == '__main__':
    main()
